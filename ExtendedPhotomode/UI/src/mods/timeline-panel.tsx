import { useValue } from "cs2/api";
import { Button, Icon, Tooltip } from "cs2/ui";
import { type ReactElement, type ReactNode, useRef, useState } from "react";
import { VC, VF, VT } from "vanilla/Components";
import { ShotListPane } from "./shot-list-panel";
import { ShotTrack } from "./shot-track";
import { ShotDragContext } from "./shot-dnd";
import {
    resetTimeline,
    setTimeline,
    shotListOpenBinding,
    timelineBinding,
    uiHiddenBinding,
} from "./shot-bindings";
import { closeButtonClass } from "./close-button";
import styles from "./timeline-panel.module.scss";
import {
    type CurveKey,
    type CurveModifier,
    canRedo,
    canUndo,
    closeTimeline,
    looping,
    toggleLoop,
    redo,
    undo,
    modifierCurves,
    moveKey,
    playing,
    removeKey,
    setTimelinePosition,
    timelineLength,
    timelineOpenBinding,
    timelinePosition,
    togglePlayback,
    transformCurves,
} from "./timeline-bindings";

/** The SVG's own coordinate space. Lanes stretch to the panel width; this is just the maths space. */
const kLaneWidth = 1000;

const kLaneHeight = 240;

/** Gridline colours, read off vanilla's own editor rather than guessed at. */
const kGrid = "#ffffff33";

// The axis label's colour and size are vanilla's too (#b9b9b9 at 11rem), but they live in the
// stylesheet rather than here — the labels are HTML, for the reason given where they are drawn.

/**
 * The five transform curves, by the ids `TransformCurveKey.ToString()` produces.
 *
 * Indexed by id rather than by position in the binding's array, because the two are not the same
 * thing: the binding is built by `GetTransformCurves`, which SKIPS any curve that is null, while the
 * write path addresses `activeSequence.transforms` unfiltered. Today all five are always present, so
 * the positions happen to agree — but a curve index derived from an array that filters is a bug
 * waiting for the first time one is not.
 */
const kTransformIds = ["PositionX", "PositionY", "PositionZ", "RotationX", "RotationY"];

const kTransformNames = ["Position X", "Position Y", "Position Z", "Pitch", "Yaw"];

/**
 * The game's own channel colours, sampled from its editor rather than approximated: X is red, Y is
 * green, Z is blue. Pitch and yaw reuse the pair that reads as "an angle" in the same palette.
 */
const kChannelColors = ["#df2e38", "#5d9c59", "#0081c9", "#e0a94a", "#b98be0"];

const kModifierColor = "#5d9c59";

/** What an inactive channel fades to. Vanilla's own value, to the digit. */
const kInactive = 0.5647;

/**
 * How the five transform curves are grouped into graphs.
 *
 * Vanilla overlays a group's channels on ONE set of axes with a legend above, rather than giving
 * each its own strip, and that is most of why its editor reads as a curve editor and a stack of
 * sparklines does not: X, Y and Z are only comparable when they share a scale.
 */
const kTransformGroups = [
    { title: "Position", members: [0, 1, 2] },
    { title: "Rotation", members: [3, 4] },
];

/** One lane, resolved: where its curve came from and how to address it when writing back. */
type Lane = {
    key: string;
    label: string;
    color: string;
    modifier: CurveModifier;
    property: boolean;
    /**
     * How the curve is addressed when writing back: a `TransformCurveKey` name for the five camera
     * channels, the modifier's own id for everything else.
     *
     * This replaced a stored curve POSITION, and a position is not an address. Vanilla drops a modifier
     * from `activeSequence.modifiers` the moment its curve loses its last key, so deleting the only
     * key on one curve shifts every lane below it — after which a position written back lands on the
     * wrong curve, or off the end of the list. The id survives that; it is the same reason the path
     * library addresses saved paths by stored id and never by row.
     */
    curveId: string;
};

/** One graph: a title, a legend, and the channels drawn together on one pair of axes. */
type Group = { key: string; title: string; channels: Lane[] };

/** The visible slice of the timeline, in seconds. */
type View = { from: number; to: number };

/**
 * A drag in progress.
 *
 * Three kinds, because they mean genuinely different things: moving keys shifts them, boxing selects
 * a span, and pulling a tangent reshapes the curve BETWEEN keys without moving any of them.
 */
type Drag =
    | {
          kind: "keys";
          /** Seconds to shift every selected key by. */
          time: number;
          /** Fraction of a lane's height; each lane turns it into its own units. */
          height: number;
          startX: number;
          startY: number;
      }
    | {
          kind: "box";
          /**
           * The graph the gesture began in, and the channel that graph was editing.
           *
           * The box picks keys from that ONE lane. It used to sweep every lane on the panel, which
           * made a drag in Position also grab the matching keys in Rotation and Focus — a selection
           * nobody asked for and, since a drag then moves the whole set, an edit to curves that were
           * never on screen. Scoping it to the active channel matches how every other edit here
           * behaves: the faded channels are context, not targets.
           */
          groupKey: string;
          laneKey: string;
          from: number;
          to: number;
          /** The vertical span, as fractions of the graph's height with 0 at the top. */
          fromY: number;
          toY: number;
          /** The graph's own rectangle, taken when the drag began — see the tangent drag's copy. */
          box: { left: number; top: number; width: number; height: number };
      }
    | {
          kind: "tangent";
          laneKey: string;
          index: number;
          side: "in" | "out";
          tangent: number;
          weight: number;
          /** Shift pulls both sides together, so a corner stays smooth. */
          unified: boolean;
          /**
           * The graph's own rectangle, taken when the drag began.
           *
           * Carried rather than derived: working out which band of the stack a lane occupies means
           * knowing how the graphs are grouped and how tall each is, and that arithmetic was already
           * wrong once when channels stopped being one-per-row.
           */
          box: { left: number; top: number; width: number; height: number };
      };

/** Handle geometry, matching vanilla's: 15px at zero weight out to 75px at full. */
const kHandleMinPx = 15;

const kHandleRangePx = 60;

const kSelected = "#ffffff";

/**
 * The frame rate a single-frame step assumes.
 *
 * A fixed 30 rather than the live frame rate: the point of stepping by a frame is to land on the
 * same time every run, and the measured rate varies with what is on screen. Vanilla's playback is
 * unlocked, so nothing downstream depends on this matching anything.
 */
const kFps = 30;

/**
 * The named ease shapes, as tangent pairs.
 *
 * Zero is flat — a curve arriving or leaving at rest — and 1 is the neutral slope Unity's own
 * smoothing produces. So "Ease in" is flat on the way in and free on the way out, and the reverse
 * for out. Linear is not zero on both sides, which would be a hold: it is free on both.
 */
const kEasings = [
    { label: "Lin", inTangent: 1, outTangent: 1, tooltip: "Linear: free on both sides, no easing." },
    { label: "In", inTangent: 0, outTangent: 1, tooltip: "Ease in: arrives at rest, leaves freely." },
    { label: "Out", inTangent: 1, outTangent: 0, tooltip: "Ease out: arrives freely, leaves at rest." },
    { label: "Both", inTangent: 0, outTangent: 0, tooltip: "Ease in and out: at rest on both sides." },
];

/** Where the mod's own icons live. One constant, so a path typo cannot differ between buttons. */
const kIcons = "coui://extendedphotomode/Camera_Icons";
const kUILIcons = "coui://uil/Standard"

/** How near a click must land to grab a key, in screen pixels. Vanilla uses 7 for the same job. */
const kGrabRadius = 9;

// The same 15–75px band expressed in viewBox units. The lane's 1000-unit box is drawn about 700px
// wide, so a unit is roughly 0.7px and the band lands near 21–107 units.
const kHandleMinReach = 21;

const kHandleReachRange = 86;

/**
 * A lane's drawing range and its true one.
 *
 * The two differ because a curve needs headroom to be drawn — a key at the exact top would be sliced
 * in half by the lane's edge, and a constant curve has no range at all to draw within. But the
 * LABELS have to report the real data: showing the padded bounds made a constant height of 592m read
 * as "651 / 532.6", which is a graph lying about its own contents.
 */
function rangeOf(keys: CurveKey[]): { low: number; high: number; dataLow: number; dataHigh: number } {
    if (keys.length === 0) {
        return { low: 0, high: 1, dataLow: 0, dataHigh: 0 };
    }

    let dataLow = keys[0].value;
    let dataHigh = keys[0].value;

    for (const key of keys) {
        dataLow = Math.min(dataLow, key.value);
        dataHigh = Math.max(dataHigh, key.value);
    }

    const span = dataHigh - dataLow;
    const pad = span < 0.0001 ? Math.max(Math.abs(dataHigh) * 0.1, 1) : span * 0.1;

    return { low: dataLow - pad, high: dataHigh + pad, dataLow, dataHigh };
}

/**
 * The range a whole group is drawn within.
 *
 * One range for every channel in the graph, because that is the point of grouping them: X, Y and Z
 * scaled independently would each fill the box and the shape of the move would be lost.
 */
function groupRange(group: Group): ReturnType<typeof rangeOf> {
    return rangeOf(group.channels.flatMap((lane) => lane.modifier.curve?.keys ?? []));
}

/**
 * Rounded values to draw gridlines and axis labels at.
 *
 * Snapped to a 1/2/5 step so the labels read as round numbers. A grid at the raw data bounds puts
 * lines at 517.3 and 692.8, which is noise pretending to be an axis.
 */
function ticks(low: number, high: number, count: number): number[] {
    const raw = (high - low) / Math.max(count, 1);

    if (!isFinite(raw) || raw <= 0) {
        return [];
    }

    const magnitude = Math.pow(10, Math.floor(Math.log10(raw)));
    const step = [1, 2, 5, 10].map((n) => n * magnitude).find((n) => n >= raw) ?? magnitude * 10;

    const out: number[] = [];

    for (let at = Math.ceil(low / step) * step; at <= high; at += step) {
        out.push(at);
    }

    return out;
}

/** Axis labels: thousands separated the way the game writes them, and no trailing noise. */
function axisLabel(value: number): string {
    const rounded = Math.abs(value) >= 100 ? Math.round(value) : Math.round(value * 100) / 100;

    return rounded.toLocaleString();
}

/** Turns a modifier id into something readable — "DepthOfField.focusDistance" is not a label. */
function prettyLabel(id: string): string {
    const tail = id.slice(id.lastIndexOf(".") + 1);
    const spaced = tail.replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/^m_/, "");

    return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

/**
 * Projects keys into the lane's own pixel space, tangents included.
 *
 * Done before handing them to vanilla's path builder rather than scaling the finished path, because
 * a non-uniform scale on an SVG distorts the stroke with it. A tangent is a gradient, so it scales by
 * the RATIO of the axes; the sign flips because SVG counts y downwards. Weights are fractions of a
 * key interval and survive untouched.
 */
function project(keys: CurveKey[], view: View, low: number, high: number): CurveKey[] {
    const sx = kLaneWidth / Math.max(view.to - view.from, 0.0001);
    const sy = kLaneHeight / Math.max(high - low, 0.0001);

    return keys.map((key) => ({
        ...key,
        time: (key.time - view.from) * sx,
        value: kLaneHeight - (key.value - low) * sy,
        inTangent: -key.inTangent * (sy / sx),
        outTangent: -key.outTangent * (sy / sx),

        // A zero weight is substituted with a third, FOR DRAWING ONLY.
        //
        // Vanilla's makeCurvePath uses the weights directly as bezier control-point positions:
        //   cp1x = lerp(prevTime, nextTime, prevOutWeight)
        //   cp1y = prevValue + (cp1x - prevTime) * outTangent
        // At weight 0 that puts cp1x exactly on prevTime, so cp1y collapses to prevValue and the
        // control point lands on the endpoint. Both ends do it, the cubic degenerates, and the
        // segment draws as a STRAIGHT LINE.
        //
        // Every key this mod generates has weight 0, because Unity's `new Keyframe(t, v)` and
        // `AddKey` both leave weights zeroed — so an orbit drew as a polygon in the editor while
        // actually playing as a smooth curve. The graph was lying about its own contents.
        //
        // A third is the exact Hermite-to-bezier equivalent of an unweighted key, which is how
        // Unity evaluates these (weightedMode is None until vanilla's MoveKeyframe forces Both on
        // the first edit). So this makes the drawing agree with the playback rather than changing
        // either — and it is applied here, on the projected copy, so nothing is written back.
        inWeight: key.inWeight || 1 / 3,
        outWeight: key.outWeight || 1 / 3,
    }));
}

/**
 * One of vanilla's Glyphs, drawn the way vanilla draws them.
 *
 * The glyphs are solid BLACK fills — they are meant to be tinted, not shown. Tinting cannot be done
 * to the `<img>` a ToolButton renders from its `src`, because a mask clips the element's background
 * and its image content alike, so the black art survives and the icon comes out black. Vanilla's
 * own `TintedIcon` renders no image at all: it is an empty box whose background is the colour and
 * whose `mask-image` is the glyph, so the shape is cut out of the tint.
 *
 * Passed as a ToolButton's child, with `.glyph` hiding the img the button still renders. Keeping the
 * button means keeping its tooltip, focus and hover behaviour rather than rebuilding all three.
 *
 * Falls back to a plain img if the module ever fails to resolve — a black icon beats no icon.
 */
function Glyph({ src }: { readonly src: string }): ReactElement {
    return VC.TintedIcon
        ? <VC.TintedIcon src={src} className={styles.glyphIcon} />
        : <img src={src} className={styles.glyphIcon} />;
}

/**
 * A text button whose tooltip actually appears.
 *
 * `Button` accepts a `tooltipLabel` prop and does NOT show one — it type-checks, it reads correctly
 * in the source, and it explains nothing to the player. The game's tooltips come from wrapping the
 * control in `Tooltip`, which is what vanilla does. IntersectionMarkingTool learned this the same
 * way and keeps the pair welded into one component for the same reason: separated, the next person
 * writes a bare Button and the tooltip silently goes missing again.
 */
function Tip({
    tooltip,
    className,
    disabled,
    onSelect,
    children,
}: {
    readonly tooltip: string;
    readonly className?: string;
    readonly disabled?: boolean;
    readonly onSelect: () => void;
    readonly children: ReactNode;
}): ReactElement {
    return (
        <Tooltip tooltip={tooltip}>
            <Button variant="flat" className={className} disabled={disabled} onSelect={onSelect}>
                {children}
            </Button>
        </Tooltip>
    );
}

/** Identifies one key across every lane, so a selection can span them. */
const idOf = (lane: Lane, index: number): string => `${lane.key}:${index}`;

function CurveGraph({
    group,
    active,
    onActivate,
    view,
    selection,
    drag,
    box,
    playhead,
    onKeyDown,
    onBackgroundDown,
    onHandleDown,
}: {
    readonly group: Group;
    readonly active: Lane;
    readonly onActivate: (lane: Lane) => void;
    readonly view: View;
    /** Where the playhead sits, as a CSS percentage across the graph. */
    readonly playhead: string;
    readonly selection: Set<string>;
    readonly drag: Drag | null;
    /** The selection band, in CSS percentages, when one is being dragged in THIS graph. */
    readonly box: { left: number; top: number; width: number; height: number } | null;
    readonly onKeyDown: (lane: Lane, index: number, event: React.MouseEvent) => void;
    readonly onBackgroundDown: (group: Group, lane: Lane, event: React.MouseEvent) => void;
    readonly onHandleDown: (
        lane: Lane,
        index: number,
        side: "in" | "out",
        event: React.MouseEvent,
    ) => void;
}): ReactElement | null {
    // Editing acts on the active channel alone, which is what the legend selects. Vanilla works the
    // same way: the others are drawn for context, faded and inert.
    const lane = active;
    const keys = lane.modifier.curve?.keys ?? [];

    if (group.channels.every((channel) => (channel.modifier.curve?.keys ?? []).length === 0)) {
        return null;
    }

    const { low, high } = groupRange(group);

    // The pending drag is applied to the drawn copy only. Committing on every mouse move would call
    // through to vanilla's Refresh each frame, which moves the live camera — fine once at the end of
    // a gesture, wrong sixty times a second during one.
    const shifted = keys.map((key, index) => {
        // A tangent drag never moves a key, it only reshapes the two segments either side of one, so
        // it previews on the dragged key alone and ignores the selection entirely.
        if (drag?.kind === "tangent") {
            if (drag.laneKey !== lane.key || drag.index !== index) {
                return key;
            }

            const both = drag.unified;

            return {
                ...key,
                inTangent: both || drag.side === "in" ? drag.tangent : key.inTangent,
                outTangent: both || drag.side === "out" ? drag.tangent : key.outTangent,
                inWeight: both || drag.side === "in" ? drag.weight : key.inWeight,
                outWeight: both || drag.side === "out" ? drag.weight : key.outWeight,
            };
        }

        if (drag?.kind !== "keys" || !selection.has(idOf(lane, index))) {
            return key;
        }

        return {
            ...key,
            time: key.time + drag.time,
            value: key.value + drag.height * (high - low),
        };
    });

    const projected = project(shifted, view, low, high);
    const path = VC.makeCurvePath ? VC.makeCurvePath(projected).join(" ") : "";

    // Tangent handles are shown for a single selected key only. With several selected the screen
    // fills with handles that cannot all be pulled at once, and the one you want stops being obvious.
    const lone = (() => {
        const picked = keys
            .map((_, index) => index)
            .filter((index) => selection.has(idOf(lane, index)));

        return picked.length === 1 ? picked[0] : -1;
    })();

    const handles =
        lone < 0
            ? []
            : (["in", "out"] as const).map((side) => {
                  const key = projected[lone];
                  const tangent = side === "in" ? key.inTangent : key.outTangent;
                  const weight = (side === "in" ? key.inWeight : key.outWeight) || 0.01;

                  // The arm's LENGTH comes from the weight alone and its DIRECTION from the tangent,
                  // which is why the step is normalised. Taking dy as tangent * dx instead would make
                  // a steep key throw its handle metres off the lane.
                  const reach = kHandleMinReach + kHandleReachRange * Math.min(Math.max(weight, 0), 1);
                  const sign = side === "in" ? -1 : 1;

                  // A vertical tangent is stored as infinity, which no arithmetic survives, so it is
                  // drawn as a straight up-or-down arm rather than normalised.
                  const [ux, uy] = isFinite(tangent)
                      ? [1 / Math.hypot(1, tangent), tangent / Math.hypot(1, tangent)]
                      : [0, Math.sign(tangent)];

                  return { side, x: key.time + sign * reach * ux, y: key.value + sign * reach * uy };
              });

    /**
     * Finds the key nearest a click, in pixels, or -1 if none is close enough.
     *
     * Hit-testing is done here rather than by putting handlers on the circles because Cohtml gives
     * SVG children no layout box and excludes them from hit testing — a circle reports a 0x0 rect at
     * the origin and `elementFromPoint` over one returns the body. Vanilla's own curve editor solves
     * it the same way, which is what its keyframes' `collisionThreshold` is for.
     */
    const nearest = (event: React.MouseEvent): { key: number; handle: "in" | "out" | null } => {
        const box = event.currentTarget.getBoundingClientRect();
        const x = event.clientX - box.left;
        const y = event.clientY - box.top;

        const sx = box.width / kLaneWidth;
        const sy = box.height / kLaneHeight;

        // Handles win ties against their own key: they sit close to it, and a handle you cannot grab
        // because the key keeps swallowing the click is worse than no handle at all.
        for (const handle of handles) {
            const dx = handle.x * sx - x;
            const dy = handle.y * sy - y;

            if (dx * dx + dy * dy < kGrabRadius * kGrabRadius) {
                return { key: lone, handle: handle.side };
            }
        }

        let best = -1;
        let closest = kGrabRadius * kGrabRadius;

        projected.forEach((key, index) => {
            const dx = key.time * sx - x;
            const dy = key.value * sy - y;
            const distance = dx * dx + dy * dy;

            if (distance < closest) {
                closest = distance;
                best = index;
            }
        });

        return { key: best, handle: null };
    };

    const rows = ticks(low, high, 4);
    const columns = ticks(view.from, view.to, 10);

    const atTime = (seconds: number): number =>
        ((seconds - view.from) / Math.max(view.to - view.from, 0.0001)) * kLaneWidth;

    const atValue = (value: number): number =>
        kLaneHeight - ((value - low) / Math.max(high - low, 0.0001)) * kLaneHeight;

    return (
        <div className={styles.group}>
            {/* Title and legend, above the graph rather than beside it — the game's arrangement, and
                it gives the curve the full width instead of losing a column to a label. */}
            <div className={styles.groupHeader}>
                <div className={styles.groupTitle}>{group.title}</div>

                <div className={styles.legend}>
                    {group.channels.map((channel) => (
                        <button
                            key={channel.key}
                            className={
                                channel.key === lane.key
                                    ? `${styles.legendItem} ${styles.legendActive}`
                                    : styles.legendItem
                            }
                            onClick={() => onActivate(channel)}
                        >
                            <div
                                className={styles.legendDot}
                                style={{ backgroundColor: channel.color }}
                            />
                            {channel.label}
                        </button>
                    ))}
                </div>
            </div>

            <div
                className={styles.graph}
                onMouseDown={(event: React.MouseEvent) => {
                    const hit = nearest(event);

                    if (hit.handle) {
                        onHandleDown(lane, hit.key, hit.handle, event);
                    } else if (hit.key >= 0) {
                        onKeyDown(lane, hit.key, event);
                    } else {
                        onBackgroundDown(group, lane, event);
                    }
                }}
                onDoubleClick={(event: React.MouseEvent) => {
                    const hit = nearest(event);

                    if (hit.key >= 0 && !hit.handle) {
                        removeKey(lane.curveId, hit.key);
                    }
                }}
            >
                <svg
                    className={styles.svg}
                    viewBox={`0 0 ${kLaneWidth} ${kLaneHeight}`}
                    preserveAspectRatio="none"
                >
                    {/* Grid first, so everything else sits on top of it. */}
                    <g stroke={kGrid} strokeWidth="1">
                        {columns.map((at) => (
                            <line key={`c${at}`} x1={atTime(at)} y1={0} x2={atTime(at)} y2={kLaneHeight} />
                        ))}

                        {rows.map((at) => (
                            <line key={`r${at}`} x1={0} y1={atValue(at)} x2={kLaneWidth} y2={atValue(at)} />
                        ))}
                    </g>

                    {/* The inactive channels, faded and drawn under the active one. They share the
                        group's axes, which is the whole reason for grouping them. */}
                    {group.channels.map((channel) => {
                        if (channel.key === lane.key) {
                            return null;
                        }

                        const other = channel.modifier.curve?.keys ?? [];

                        if (other.length === 0 || !VC.makeCurvePath) {
                            return null;
                        }

                        return (
                            <path
                                key={channel.key}
                                d={VC.makeCurvePath(project(other, view, low, high)).join(" ")}
                                fill="none"
                                stroke={channel.color}
                                strokeWidth="1.2"
                                opacity={kInactive}
                            />
                        );
                    })}

                    <path d={path} fill="none" stroke={lane.color} strokeWidth="1.5" />

                    {/* Handle arms. Lines survive the stretch; the grips that cap them do not, so
                        those are HTML below. */}
                    {handles.map((handle) => (
                        <line
                            key={handle.side}
                            x1={projected[lone].time}
                            y1={projected[lone].value}
                            x2={handle.x}
                            y2={handle.y}
                            stroke={kSelected}
                            strokeWidth="1"
                            opacity="0.55"
                        />
                    ))}
                </svg>

                {/* Markers and labels are positioned HTML, not SVG.
                    The viewBox is stretched to the panel's width with preserveAspectRatio="none", so
                    anything with a SHAPE drawn in it comes out anisotropically scaled — a circle
                    renders as an oval and text as squashed glyphs. Vanilla never hits this because
                    its viewBox is 1:1 with its pixel box. Percent-positioned HTML is immune, and it
                    is hit-testable besides, which SVG children are not under Cohtml. */}
                {rows.map((at) => (
                    <div
                        key={`l${at}`}
                        className={styles.axisLabel}
                        style={{ top: `${(atValue(at) / kLaneHeight) * 100}%` }}
                    >
                        {axisLabel(at)}
                    </div>
                ))}

                {/* The selection band, drawn in the graph it belongs to rather than across the whole
                    stack — it selects within this one channel, so it has to look like it does. */}
                {box && (
                    <div
                        className={styles.box}
                        style={{
                            left: `${box.left}%`,
                            top: `${box.top}%`,
                            width: `${box.width}%`,
                            height: `${box.height}%`,
                        }}
                    />
                )}

                {handles.map((handle) => (
                    <div
                        key={`h${handle.side}`}
                        className={styles.handle}
                        style={{
                            left: `${(handle.x / kLaneWidth) * 100}%`,
                            top: `${(handle.y / kLaneHeight) * 100}%`,
                        }}
                    />
                ))}

                {projected.map((key, index) => {
                    const picked = selection.has(idOf(lane, index));

                    return (
                        <div
                            key={index}
                            className={picked ? `${styles.key} ${styles.keyPicked}` : styles.key}
                            style={{
                                left: `${(key.time / kLaneWidth) * 100}%`,
                                top: `${(key.value / kLaneHeight) * 100}%`,
                                backgroundColor: picked ? kSelected : lane.color,
                            }}
                        />
                    );
                })}

                {/* The playhead lives INSIDE each plot area rather than spanning the whole stack.
                    One line across `.lanes` also crossed the group headers, the legends and the gaps
                    between graphs, so it read as a divider laid over the panel instead of a time
                    marker on the data. Confined here, it only ever crosses something it is marking a
                    time in. Last child, so it draws over the curves and keys. */}
                <div className={styles.playhead} style={{ left: playhead }} />
            </div>
        </div>
    );
}

/**
 * A replacement for the vanilla cinematic curve editor.
 *
 * Reads and writes the game's own `cinematicCamera` bindings, so it is a different VIEW of the same
 * sequence rather than a parallel model — vanilla's panel keeps working alongside it, and playback,
 * scrubbing and saving are untouched.
 *
 * What it does that vanilla's does not: every channel is scaled to its own values instead of sharing
 * one axis, the labels show the real range rather than the modifier's declared bounds, all curves
 * stack under one playhead, the time axis zooms, and keys can be selected in bulk and dragged.
 */
export function TimelinePanel(): ReactElement | null {
    const open = useValue(timelineOpenBinding.binding);
    const transforms = useValue(transformCurves);
    const modifiers = useValue(modifierCurves);
    const position = useValue(timelinePosition);
    const length = useValue(timelineLength);
    const isPlaying = useValue(playing);
    const listOpen = useValue(shotListOpenBinding.binding);
    const hidden = useValue(uiHiddenBinding.binding);
    const timeline = useValue(timelineBinding.binding);

    const undoable = useValue(canUndo);
    const loop = useValue(looping);
    const redoable = useValue(canRedo);

    /**
     * The work area: the span being worked on, in seconds, or null for the whole sequence.
     *
     * Separate from `zoom` on purpose. Zoom is what you can SEE and the work area is what you are
     * WORKING ON, and conflating them is a mistake every timeline that tries it regrets — you cannot
     * zoom out to check context without losing the range you had set.
     */
    const [workArea, setWorkArea] = useState<View | null>(null);

    /** Snap the playhead and dragged keys to existing keys. */
    const [snap, setSnap] = useState(true);

    const [selection, setSelection] = useState<Set<string>>(new Set());
    const [drag, setDrag] = useState<Drag | null>(null);
    const [zoom, setZoom] = useState<View | null>(null);

    /** Which channel each graph is editing, by group key. Absent means the group's first. */
    const [active, setActive] = useState<Record<string, string>>({});

    // Scrubbing is tracked from the panel root rather than the strip itself. A horizontal drag along
    // something this thin wanders off it constantly, and handlers on the strip alone would drop the
    // gesture the moment the pointer left — which is exactly what made it feel like it did not drag.
    const scrubRef = useRef<HTMLDivElement | null>(null);
    const [scrubbing, setScrubbing] = useState(false);

    // Either toggle opens the one window; the shot list's own toggle just decides whether its pane
    // starts expanded. Both entry points keep working, which they would not if they were merged into
    // a single binding.
    //
    // Hidden wins over both, and the window is unmounted rather than made transparent — a panel that
    // is merely invisible still swallows the clicks and the wheel over the picture behind it.
    if (hidden || (!open && !listOpen)) {
        return null;
    }

    const closeWindow = (): void => {
        closeTimeline();
        shotListOpenBinding.set(false);
    };

    const span = Math.max(length, 0.0001);
    const view: View = zoom ?? { from: 0, to: span };
    const width = Math.max(view.to - view.from, 0.0001);

    const lanes: Lane[] = [
        ...transforms.map((modifier: CurveModifier, index: number) => {
            const canonical = kTransformIds.indexOf(modifier.id);
            const at = canonical >= 0 ? canonical : index;

            return {
                key: `t${at}`,
                label: kTransformNames[at] ?? modifier.id,
                color: kChannelColors[at % kChannelColors.length],
                modifier,
                property: false,

                // The canonical name for the slot, not the id it arrived with: GetTransformCurves
                // COMPACTS null curves out of the array it pushes, so an arriving position means
                // nothing on its own and the id is the only thing that pins the channel down.
                curveId: kTransformIds[at] ?? modifier.id,
            };
        }),
        ...modifiers.map((modifier: CurveModifier, index: number) => ({
            key: `m${index}`,
            label: prettyLabel(modifier.id),
            color: kModifierColor,
            modifier,
            property: true,
            curveId: modifier.id,
        })),
    ];

    // Transforms are gathered into Position and Rotation graphs the way the game groups them; every
    // property modifier is its own graph, since nothing else shares its units.
    const groups: Group[] = [
        ...kTransformGroups.map((spec) => ({
            key: spec.title,
            title: spec.title,
            channels: spec.members
                .map((at) => lanes.find((lane) => lane.key === `t${at}`))
                .filter((lane): lane is Lane => Boolean(lane)),
        })).filter((group) => group.channels.length > 0),
        ...lanes
            .filter((lane) => lane.property)
            .map((lane) => ({ key: lane.key, title: lane.label, channels: [lane] })),
    ];

    const activeOf = (group: Group): Lane =>
        group.channels.find((lane) => lane.key === active[group.key]) ?? group.channels[0];

    /**
     * The value range a lane is DRAWN within — its group's, not its own.
     *
     * Every drag converts a fraction of the graph's height into a value, so it has to use the range
     * that graph was drawn with or the commit disagrees with the preview. Position X spans a couple
     * of hundred metres on its own but shares a 0–4,000 axis with Y and Z, so scaling by the lane's
     * own range moved a dragged key a twentieth as far as the preview showed and it appeared to snap
     * back to where it started.
     */
    const rangeFor = (lane: Lane): ReturnType<typeof rangeOf> => {
        const group = groups.find((candidate) =>
            candidate.channels.some((channel) => channel.key === lane.key),
        );

        return group ? groupRange(group) : rangeOf(lane.modifier.curve?.keys ?? []);
    };

    /**
     * Every key time on the timeline, sorted and de-duplicated.
     *
     * The snap targets, and what the step-to-next-key transport moves between. Rounded before being
     * de-duplicated because keys written by different generators land on times that differ in the
     * seventh decimal, which would otherwise leave two snap targets a micro-second apart.
     */
    const keyTimes: number[] = (() => {
        const seen = new Set<number>();

        for (const lane of lanes) {
            for (const key of lane.modifier.curve?.keys ?? []) {
                seen.add(Math.round(key.time * 1000) / 1000);
            }
        }

        return [...seen].sort((a, b) => a - b);
    })();

    /**
     * The nearest key time, if one is close enough to be worth snapping to.
     *
     * `exclude` is the time of the key being dragged. Without it a dragged key snaps to its OWN
     * entry in the list — it is still there until the edit commits — and so refuses to move at all.
     */
    const snapped = (time: number, exclude?: number): number => {
        if (!snap || keyTimes.length === 0) {
            return time;
        }

        // A tolerance in SECONDS scaled to the zoom, so the snap stays the same distance on screen
        // however far in you are. A fixed tolerance in seconds becomes unusably sticky when zoomed in
        // and useless when zoomed out.
        const tolerance = width * 0.01;

        let best = time;
        let closest = tolerance;

        for (const at of keyTimes) {
            if (exclude !== undefined && Math.abs(at - exclude) < 0.0011) {
                continue;
            }

            const distance = Math.abs(at - time);

            if (distance < closest) {
                closest = distance;
                best = at;
            }
        }

        return best;
    };

    /**
     * The one selected key, if the selection is exactly one — what the inspector edits.
     *
     * Deliberately not "the first of several": showing one key's exact numbers while five are
     * selected invites you to type a value and watch it apply to a key you were not looking at.
     */
    const inspected: { lane: Lane; index: number; key: CurveKey } | null = (() => {
        if (selection.size !== 1) {
            return null;
        }

        for (const lane of lanes) {
            const keys = lane.modifier.curve?.keys ?? [];

            for (let index = 0; index < keys.length; index++) {
                if (selection.has(idOf(lane, index))) {
                    return { lane, index, key: keys[index] };
                }
            }
        }

        return null;
    })();

    /**
     * How much one press of the value stepper moves the key.
     *
     * A fraction of the graph's own range rather than a fixed amount, because these lanes are not
     * in the same units — a metre is a nudge on a position curve and a whole shot on a focus one.
     */
    const inspectorStep = (() => {
        if (!inspected) {
            return 1;
        }

        const { low, high } = rangeFor(inspected.lane);

        return Math.max((high - low) / 100, 0.001);
    })();

    const nudge = (byTime: number, byValue: number): void => {
        if (!inspected) {
            return;
        }

        moveKey(inspected.lane.curveId, inspected.index, {
            ...inspected.key,
            time: Math.max(inspected.key.time + byTime, 0),
            value: inspected.key.value + byValue,
        });
    };

    /** Writes a named ease shape onto the selected key's tangents. */
    const applyEase = (inTangent: number, outTangent: number): void => {
        if (!inspected) {
            return;
        }

        moveKey(inspected.lane.curveId, inspected.index, {
            ...inspected.key,
            inTangent,
            outTangent,
            // Weights back to the neutral third. An ease preset that inherited whatever weights were
            // there would not reliably produce the shape it is named after.
            inWeight: 0.333,
            outWeight: 0.333,
        });
    };

    /** Moves the playhead to the next or previous key. */
    const stepKey = (direction: number): void => {
        const next =
            direction > 0
                ? keyTimes.find((at) => at > position + 0.001)
                : [...keyTimes].reverse().find((at) => at < position - 0.001);

        if (next !== undefined) {
            setTimelinePosition(next);
        }
    };

    /** Nudges the playhead by one frame at the project rate. */
    const stepFrame = (direction: number): void =>
        setTimelinePosition(Math.min(Math.max(position + direction / kFps, 0), span));

    /** Seeks to whatever time sits under an absolute cursor x, measured against the strip. */
    const seekTo = (clientX: number): void => {
        const box = scrubRef.current?.getBoundingClientRect();

        if (!box) {
            return;
        }

        const fraction = Math.min(Math.max((clientX - box.left) / Math.max(box.width, 1), 0), 1);

        setTimelinePosition(snapped(view.from + fraction * width));
    };

    /** Zooms about the cursor, so whatever is under the pointer stays under it. */
    const onWheel = (event: React.WheelEvent): void => {
        event.stopPropagation();

        const box = event.currentTarget.getBoundingClientRect();
        const at = view.from + ((event.clientX - box.left) / Math.max(box.width, 1)) * width;
        const scale = event.deltaY > 0 ? 1.2 : 1 / 1.2;

        const from = at - (at - view.from) * scale;
        const to = at + (view.to - at) * scale;

        // Never wider than the whole sequence, never narrower than a tenth of a second, and never
        // scrolled off either end — a view you cannot find your way back from is worse than no zoom.
        if (to - from >= span) {
            setZoom(null);
            return;
        }

        const clamped = Math.max(to - from, 0.1);
        const start = Math.min(Math.max(from, 0), span - clamped);

        setZoom({ from: start, to: start + clamped });
    };

    const beginKeyDrag = (lane: Lane, index: number, event: React.MouseEvent): void => {
        const id = idOf(lane, index);
        const next = new Set(event.shiftKey ? selection : []);

        if (event.shiftKey && next.has(id)) {
            next.delete(id);
        } else {
            next.add(id);
        }

        // Dragging one of an existing selection moves the whole set; dragging an unselected key
        // replaces the selection with it, which is how every editor behaves.
        setSelection(selection.has(id) && !event.shiftKey ? selection : next);
        setDrag({ kind: "keys", time: 0, height: 0, startX: event.clientX, startY: event.clientY });
    };

    const beginBox = (group: Group, lane: Lane, event: React.MouseEvent): void => {
        const box = event.currentTarget.getBoundingClientRect();
        const at = view.from + ((event.clientX - box.left) / Math.max(box.width, 1)) * width;
        const y = (event.clientY - box.top) / Math.max(box.height, 1);

        if (!event.shiftKey) {
            setSelection(new Set());
        }

        setDrag({
            kind: "box",
            groupKey: group.key,
            laneKey: lane.key,
            from: at,
            to: at,
            fromY: y,
            toY: y,
            box: { left: box.left, top: box.top, width: box.width, height: box.height },
        });
    };

    /**
     * Starts a tangent drag.
     *
     * The key's current tangent and weight are carried in the drag rather than re-read on each move,
     * so a gesture that never travels far enough to change anything commits exactly what was there.
     */
    const beginHandle = (
        lane: Lane,
        index: number,
        side: "in" | "out",
        event: React.MouseEvent,
    ): void => {
        const key = (lane.modifier.curve?.keys ?? [])[index];

        if (!key) {
            return;
        }

        const box = event.currentTarget.getBoundingClientRect();

        setDrag({
            kind: "tangent",
            laneKey: lane.key,
            index,
            side,
            tangent: side === "in" ? key.inTangent : key.outTangent,
            weight: side === "in" ? key.inWeight : key.outWeight,
            unified: event.shiftKey,
            box: { left: box.left, top: box.top, width: box.width, height: box.height },
        });
    };

    const onMove = (event: React.MouseEvent): void => {
        if (!drag) {
            return;
        }

        const box = event.currentTarget.getBoundingClientRect();
        const perPixel = width / Math.max(box.width, 1);

        if (drag.kind === "keys") {
            setDrag({
                ...drag,
                time: (event.clientX - drag.startX) * perPixel,
                // A fraction of the lanes block rather than of one lane, so a slow drag is a fine
                // nudge whatever the panel's height happens to be.
                height: -(event.clientY - drag.startY) / Math.max(box.height, 1),
            });

            return;
        }

        if (drag.kind === "box") {
            // Against the GRAPH's rectangle, not the panel's. The gesture is tracked from the panel
            // so it survives the pointer leaving the graph, but both ends of the box have to be
            // measured in the same space or the band drifts away from the cursor vertically.
            setDrag({
                ...drag,
                to:
                    view.from +
                    ((event.clientX - drag.box.left) / Math.max(drag.box.width, 1)) * width,
                toY: (event.clientY - drag.box.top) / Math.max(drag.box.height, 1),
            });

            return;
        }

        const lane = lanes.find((candidate) => candidate.key === drag.laneKey);
        const keys = lane?.modifier.curve?.keys ?? [];
        const key = keys[drag.index];

        if (!lane || !key) {
            return;
        }

        // Vanilla's scheme, followed exactly so a curve shaped here reads the same in its own editor:
        // the tangent is the gradient in CURVE space, while the weight comes from how far the handle
        // was pulled in SCREEN pixels — 15px is weightless, 75px is full.
        const { low, high } = rangeFor(lane);
        const graph = drag.box;

        const atTime =
            view.from + ((event.clientX - graph.left) / Math.max(graph.width, 1)) * width;
        const atValue =
            high - ((event.clientY - graph.top) / Math.max(graph.height, 1)) * (high - low);

        let dx = atTime - key.time;
        const dy = atValue - key.value;

        // Each handle stays on its own side of its key. Letting one cross would invert the segment it
        // controls, which Unity renders as a curve folding back through itself.
        if ((drag.side === "in" && dx > 0) || (drag.side === "out" && dx < 0)) {
            dx = 0;
        }

        const pixels = Math.hypot(
            (dx / Math.max(width, 0.0001)) * graph.width,
            (dy / Math.max(high - low, 0.0001)) * graph.height,
        );

        // A perfectly vertical pull is a tangent of infinity, which is what Unity stores for a key
        // that rises with no run. Dividing by a dx of zero already gives exactly that, but the sign
        // has to come from dy or a downward pull reads as +Infinity.
        const tangent = dx === 0 ? (dy >= 0 ? Infinity : -Infinity) : dy / dx;

        const reach = Math.min(Math.max(pixels, kHandleMinPx), kHandleMinPx + kHandleRangePx);

        setDrag({
            ...drag,
            tangent,
            weight: Math.max((reach - kHandleMinPx) / kHandleRangePx, 0.01),
        });
    };

    /**
     * Commits the drag.
     *
     * Keys within a lane are written in the direction of travel — rightmost first when moving later,
     * leftmost first when moving earlier. AnimationCurve keeps its keys sorted by time, so a key that
     * crosses a sibling changes both their indices; going in the direction of movement means nothing
     * is ever written through an index that a previous write has already invalidated.
     */
    const commit = (): void => {
        if (!drag) {
            return;
        }

        if (drag.kind === "tangent") {
            const lane = lanes.find((candidate) => candidate.key === drag.laneKey);
            const key = (lane?.modifier.curve?.keys ?? [])[drag.index];

            if (!lane || !key) {
                return;
            }

            // Shift pulls both sides together, which is how a key is kept smooth through; without it
            // only the dragged side moves and the key becomes a corner. Same as vanilla's editor.
            const both = drag.unified;

            moveKey(lane.curveId, drag.index, {
                ...key,
                inTangent: both || drag.side === "in" ? drag.tangent : key.inTangent,
                outTangent: both || drag.side === "out" ? drag.tangent : key.outTangent,
                inWeight: both || drag.side === "in" ? drag.weight : key.inWeight,
                outWeight: both || drag.side === "out" ? drag.weight : key.outWeight,
            });

            // A tangent edit moves nothing, so no index can have gone stale and the selection stands —
            // which matters, since the handles are only drawn while the key is selected.
            return;
        }

        if (drag.kind !== "keys" || (Math.abs(drag.time) < 0.0001 && Math.abs(drag.height) < 0.0001)) {
            return;
        }

        for (const lane of lanes) {
            const keys = lane.modifier.curve?.keys ?? [];
            const { low, high } = rangeFor(lane);

            const picked = keys
                .map((_, index) => index)
                .filter((index) => selection.has(idOf(lane, index)));

            if (picked.length === 0) {
                continue;
            }

            const ordered = drag.time >= 0 ? picked.slice().reverse() : picked;

            for (const index of ordered) {
                const key = keys[index];

                // The binding can lag a delete or a regenerate by a frame, and writing through an
                // index the curve no longer has throws inside Unity rather than failing quietly.
                if (!key) {
                    continue;
                }

                // Snapping applies to a single key only. Snapping each of a multi-key drag
                // independently would pull them onto different targets and destroy the spacing
                // between them, which is the one thing a bulk drag must preserve.
                const moved = Math.max(key.time + drag.time, 0);

                moveKey(lane.curveId, index, {
                    ...key,
                    time: ordered.length === 1 ? snapped(moved, key.time) : moved,
                    value: key.value + drag.height * (high - low),
                });
            }
        }

        // The indices we hold are stale the moment a key reorders, so the selection is dropped rather
        // than left pointing at whatever now sits at those positions.
        setSelection(new Set());
    };

    const finish = (): void => {
        if (drag?.kind === "box") {
            const from = Math.min(drag.from, drag.to);
            const to = Math.max(drag.from, drag.to);
            const top = Math.min(drag.fromY, drag.toY);
            const bottom = Math.max(drag.fromY, drag.toY);
            const lane = lanes.find((candidate) => candidate.key === drag.laneKey);
            const next = new Set(selection);

            if (lane) {
                // The same axes the graph drew the keys on, so what the band visibly encloses is what
                // it selects. A lane is scaled to its GROUP's range, not its own — see rangeFor.
                const { low, high } = rangeFor(lane);

                (lane.modifier.curve?.keys ?? []).forEach((key, index) => {
                    const y = 1 - (key.value - low) / Math.max(high - low, 0.0001);

                    if (key.time >= from && key.time <= to && y >= top && y <= bottom) {
                        next.add(idOf(lane, index));
                    }
                });
            }

            setSelection(next);
        } else {
            commit();
        }

        setDrag(null);
    };

    const playhead = `${Math.min(Math.max((position - view.from) / width, 0), 1) * 100}%`;

    return (
        <div
            className={styles.panel}
            onMouseMove={(event: React.MouseEvent) => {
                if (!scrubbing) {
                    return;
                }

                // A release outside the panel never reaches onMouseUp, and the gesture would stick:
                // the playhead then follows the cursor around for the rest of the session. Seen for
                // real. `buttons` is the reliable signal here — it survives this engine's event
                // marshalling even though clientX does not on a synthesised event.
                if ((event.buttons & 1) === 0) {
                    setScrubbing(false);
                    return;
                }

                seekTo(event.clientX);
            }}
            onMouseUp={() => setScrubbing(false)}
            onMouseLeave={() => setScrubbing(false)}
        >
            <div className={styles.header}>
                {/* Collapsing the shot list rather than closing a separate window: the two are ends
                    of one pipeline, but you rarely need both at full size at once. */}
                <Tip
                    tooltip={listOpen
                        ? "Collapse the shot list, giving the curves the full width."
                        : "Show the shot list beside the curves."}
                    className={styles.play}
                    onSelect={() => shotListOpenBinding.set(!listOpen)}
                >
                    {/* One Icon with a varying src, not two Icons in a ternary. A ternary branch is
                        a single expression, so an icon AND a label would each have to be wrapped in
                        a fragment — and then "Shots" would be written twice for no reason, since
                        only the arrow changes. */}
                    <Icon
                        src={listOpen
                            ? "Media/PhotoMode/ExpandLeft.svg"
                            : "Media/PhotoMode/ExpandRight.svg"}
                        tinted
                        className={styles.labelIcon}
                    />
                    Shots
                </Tip>

                <span className={styles.title}>TIMELINE</span>

                {/* Transport. Ordered the way a deck is: jump, step, play, step, jump.

                    ToolButton, not Button. `src` is on IconButtonProps — plain Button has no such
                    prop, so it lands on the underlying <button> element where it means nothing, and
                    a self-closing Button with no children renders empty. The paths also need their
                    .svg: coui:// does not guess extensions. */}
                <VC.ToolButton
                    src={`${kIcons}/PlayAheads_PlayAheadLeft2.svg`}
                    tooltip="Jump to the previous keyframe."
                    focusKey={VF.FOCUS_DISABLED}
                    className={styles.step}
                    onSelect={() => stepKey(-1)}
                />

                <VC.ToolButton
                    src={`${kIcons}/PlayAheads_PlayAheadLeft1.svg`}
                    tooltip="Step back one frame."
                    focusKey={VF.FOCUS_DISABLED}
                    className={styles.step}
                    onSelect={() => stepFrame(-1)}
                />

                <Tip
                    tooltip={isPlaying ? "Stop playback." : "Play the sequence from the playhead."}
                    className={styles.play}
                    onSelect={togglePlayback}
                >
                    {isPlaying ? <Icon src={"Media/PhotoMode/Stop.svg"} tinted className={styles.closeIcon} /> : <Icon src={"Media/PhotoMode/Play.svg"} tinted className={styles.closeIcon} />}
                </Tip>

                <VC.ToolButton
                    src={`${kIcons}/PlayAheads_PlayAheadRight1.svg`}
                    tooltip="Step forward one frame."
                    focusKey={VF.FOCUS_DISABLED}
                    className={styles.step}
                    onSelect={() => stepFrame(1)}
                />

                {/* Note the lower-case "a": the file really is PlayaheadRight2.svg while its three
                    siblings are PlayAhead. coui:// paths are case-sensitive, so this has to keep
                    matching the file exactly. Worth renaming the asset to match the others. */}
                <VC.ToolButton
                    src={`${kIcons}/PlayAheads_PlayaheadRight2.svg`}
                    tooltip="Jump to the next keyframe."
                    focusKey={VF.FOCUS_DISABLED}
                    className={styles.step}
                    onSelect={() => stepKey(1)}
                />


                <Tip
                    tooltip="Snap the playhead and dragged keys to existing keyframes."
                    className={snap ? `${styles.step} ${styles.active}` : styles.step}
                    onSelect={() => setSnap(!snap)}
                >
                    Snap
                </Tip>

                {/* Loop is not merely a playback flag. Switching it on runs EnsureLoop, which forces
                    the last key of every curve to the first key's value — a real edit, and the reason
                    dragging the last key appears to snap back while it is on.

                    Both icons are vanilla's own choices for these two actions, read off the
                    cinematic camera panel: it draws loop as LoopOn/LoopOff and reset as the Trash
                    glyph. Carrying the on/off pair rather than tinting one icon means the button
                    reads as toggled even where the active background is subtle. */}
                <VC.ToolButton
                    src={loop ? "Media/PhotoMode/LoopOn.svg" : "Media/PhotoMode/LoopOff.svg"}
                    tooltip="Loop playback, holding the last keyframe of every curve to match the first."
                    focusKey={VF.FOCUS_DISABLED}
                    className={loop
                        ? `${styles.step} ${styles.glyph} ${styles.active}`
                        : `${styles.step} ${styles.glyph}`}
                    onSelect={() => toggleLoop(!loop)}
                >
                    <Glyph src={loop ? "Media/PhotoMode/LoopOn.svg" : "Media/PhotoMode/LoopOff.svg"} />
                </VC.ToolButton>

                <VC.ToolButton
                    src="Media/Glyphs/Trash.svg"
                    tooltip="Clear every curve on the timeline. Undo brings it back."
                    focusKey={VF.FOCUS_DISABLED}
                    className={`${styles.step} ${styles.glyph}`}
                    onSelect={resetTimeline}
                >
                    <Glyph src="Media/Glyphs/Trash.svg" />
                </VC.ToolButton>


                <VC.ToolButton
                    src={`${kUILIcons}/ArrowCircularLeft.svg`}
                    tooltip="Undo the last edit."
                    focusKey={VF.FOCUS_DISABLED}
                    className={styles.step}
                    disabled={!undoable}
                    onSelect={undo}
                />

                <VC.ToolButton
                    src={`${kUILIcons}/ArrowCircularRight.svg`}
                    tooltip="Redo the last undone edit."
                    focusKey={VF.FOCUS_DISABLED}
                    className={styles.step}
                    disabled={!redoable}
                    onSelect={redo}
                />    
                

                <span className={styles.clock}>
                    {/* One string, not several. Cohtml breaks between adjacent text nodes whatever
                        white-space says, which put the separator on its own line. */}
                    {`${Math.round(position * 10) / 10}s / ${Math.round(span * 10) / 10}s` +
                        (zoom ? ` · showing ${Math.round(view.from)}–${Math.round(view.to)}s` : "") +
                        (selection.size > 0 ? ` · ${selection.size} selected` : "")}
                </span>

                {/* Retiming lives here now, next to the clock it changes. It was a row on the shot
                    list, which showed none of the axis it rescales. */}
                {timeline.keyframes > 0 && (
                    <div className={styles.retimeGroup}>
                        <Tip
                            tooltip="Shorten the whole sequence by a second, rescaling every key."
                            className={styles.retimeStep}
                            onSelect={() => setTimeline("duration", Math.max(Math.round(timeline.duration) - 1, 1))}
                        >
                           <Icon src={`${kUILIcons}/Minus.svg`} tinted className={styles.closeIcon} />
                        </Tip>

                        <span className={styles.retime}>{`${Math.round(timeline.duration)}s`}</span>

                        <Tip
                            tooltip="Lengthen the whole sequence by a second, rescaling every key."
                            className={styles.retimeStep}
                            onSelect={() => setTimeline("duration", Math.round(timeline.duration) + 1)}
                        >
                            <Icon src={`${kUILIcons}/Plus.svg`} tinted className={styles.closeIcon} />
                        </Tip>
                    </div>
                )}

                {/* In and out points. They mark the span being worked on; "Zoom" then makes the view
                    match it, which is the one place work area and zoom should meet. */}
                <Tip
                    tooltip="Set the work area's start to the playhead."
                    className={styles.step}
                    onSelect={() =>
                        setWorkArea({ from: position, to: Math.max(workArea?.to ?? span, position) })
                    }
                >
                    In
                </Tip>

                <Tip
                    tooltip="Set the work area's end to the playhead."
                    className={styles.step}
                    onSelect={() =>
                        setWorkArea({ from: Math.min(workArea?.from ?? 0, position), to: position })
                    }
                >
                    Out
                </Tip>

                {workArea && (
                    <>
                        <Tip
                            tooltip="Zoom the view to the work area."
                            className={styles.step}
                            onSelect={() => setZoom({ ...workArea })}
                        >
                            Zoom
                        </Tip>

                        <Tip
                            tooltip="Clear the work area."
                            className={styles.step}
                            onSelect={() => setWorkArea(null)}
                        >
                            <Icon src="Media/Glyphs/Close.svg" tinted className={styles.closeIcon} />
                        </Tip>
                    </>
                )}

                {zoom && (
                    <Tip
                        tooltip="Fit the whole sequence in the view."
                        className={styles.play}
                        onSelect={() => setZoom(null)}
                    >
                        Fit
                    </Tip>
                )}

                {/* The game's own close button, not a "×" character.
                    Taking vanilla's `closeButton` class off the panel theme brings its hover, its
                    circular hit area and its sizing with it — none of which a styled text glyph had.
                    The icon is a tinted mask over Media/Glyphs/Close.svg, which is exactly what
                    vanilla renders inside its own close buttons. */}
                <Tooltip tooltip="Close the timeline and the shot list.">
                    <Button
                        variant="icon"
                        className={closeButtonClass(styles.close)}
                        focusKey={VF.FOCUS_DISABLED}
                        onSelect={closeWindow}
                    >
                        <Icon src="Media/Glyphs/Close.svg" tinted className={styles.closeIcon} />
                    </Button>
                </Tooltip>
            </div>

            {/* One drag context around both panes: dragging a shot from the list onto the track is
                the same gesture as reordering within either, and only works if they share it. */}
            <ShotDragContext>
            <div className={styles.body}>
                {listOpen && <ShotListPane />}

                <div className={styles.curves}>

            {/* One mouse-move and one mouse-up for the whole block: a gesture that starts on a key
                often ends off it, and per-key handlers would drop the drag the moment it left. */}
            <div
                className={styles.lanes}
                onWheel={onWheel}
                onMouseMove={onMove}
                onMouseUp={finish}
                onMouseLeave={finish}
            >
                {/* Above the graphs and inside the same block, so it shares their time axis and the
                    one playhead crosses it too — which is the whole reason the shots are worth
                    drawing here rather than as a list of times. */}
                <ShotTrack view={view} width={width} playhead={playhead} />

                {groups.map((group) => (
                    <CurveGraph
                        key={group.key}
                        group={group}
                        active={activeOf(group)}
                        onActivate={(lane: Lane) => {
                            // Switching channel drops the selection: the ids it holds point at keys
                            // in the channel being left, which is no longer the one being edited.
                            setSelection(new Set());
                            setActive({ ...active, [group.key]: lane.key });
                        }}
                        view={view}
                        playhead={playhead}
                        selection={selection}
                        drag={drag && drag.kind !== "box" ? drag : null}
                        box={
                            drag?.kind === "box" && drag.groupKey === group.key
                                ? {
                                      left:
                                          ((Math.min(drag.from, drag.to) - view.from) / width) * 100,
                                      width: (Math.abs(drag.to - drag.from) / width) * 100,
                                      top: Math.min(drag.fromY, drag.toY) * 100,
                                      height: Math.abs(drag.toY - drag.fromY) * 100,
                                  }
                                : null
                        }
                        onKeyDown={beginKeyDrag}
                        onBackgroundDown={beginBox}
                        onHandleDown={beginHandle}
                    />
                ))}

                {/* The work area, dimming what is OUTSIDE it rather than tinting what is inside —
                    the span you are working on should look normal and the rest should recede. */}
                {workArea && (
                    <>
                        <div
                            className={styles.outside}
                            style={{
                                left: 0,
                                width: `${Math.min(Math.max((workArea.from - view.from) / width, 0), 1) * 100}%`,
                            }}
                        />

                        <div
                            className={styles.outside}
                            style={{
                                left: `${Math.min(Math.max((workArea.to - view.from) / width, 0), 1) * 100}%`,
                                right: 0,
                            }}
                        />
                    </>
                )}

            </div>

            {/* A ruler, because a curve without a time axis only tells you the shape and never the
                when — and the whole point of zooming is to work on a particular moment. */}
            <div className={styles.ruler}>
                {ticks(view.from, view.to, 10).map((at) => (
                    <span
                        key={at}
                        className={styles.tick}
                        style={{ left: `${((at - view.from) / width) * 100}%` }}
                    >
                        {`${Math.round(at * 100) / 100}s`}
                    </span>
                ))}
            </div>

                    {/* The key inspector. Only for a single selection: these are exact values for
                        one key, and showing them for several would have to average or blank them,
                        neither of which is a number anyone can act on. */}
                    {inspected ? (
                        <div className={styles.inspector}>
                            <span className={styles.inspectorLabel}>{inspected.lane.label}</span>

                            <span className={styles.inspectorField}>
                                {`t ${Math.round(inspected.key.time * 1000) / 1000}s`}
                            </span>

                            <Tip
                                tooltip="Move this key one frame earlier."
                                className={styles.step}
                                onSelect={() => nudge(-1 / kFps, 0)}
                            >
                                ‹
                            </Tip>

                            <Tip
                                tooltip="Move this key one frame later."
                                className={styles.step}
                                onSelect={() => nudge(1 / kFps, 0)}
                            >
                                ›
                            </Tip>

                            <span className={styles.inspectorField}>
                                {`v ${Math.round(inspected.key.value * 1000) / 1000}`}
                            </span>

                            <Tip
                                tooltip="Lower this key's value by a hundredth of the graph's range."
                                className={styles.step}
                                onSelect={() => nudge(0, -inspectorStep)}
                            >
                                −
                            </Tip>

                            <Tip
                                tooltip="Raise this key's value by a hundredth of the graph's range."
                                className={styles.step}
                                onSelect={() => nudge(0, inspectorStep)}
                            >
                                +
                            </Tip>

                            {/* Ease presets. The tangent handles can express anything these can and
                                more, but naming the four shapes people actually ask for is faster
                                than dragging to them, and it is how they are described in a brief. */}
                            {kEasings.map((ease) => (
                                <Tip
                                    key={ease.label}
                                    tooltip={ease.tooltip}
                                    className={styles.step}
                                    onSelect={() => applyEase(ease.inTangent, ease.outTangent)}
                                >
                                    {ease.label}
                                </Tip>
                            ))}

                            <Tip
                                tooltip="Delete this keyframe. Double-clicking it on the graph does the same."
                                className={styles.play}
                                onSelect={() =>
                                    removeKey(inspected.lane.curveId, inspected.index)
                                }
                            >
                                Delete
                            </Tip>
                        </div>
                    ) : (
                        <div className={styles.footer}>
                            {selection.size > 0
                                ? `${selection.size} keys selected — drag to move, double-click to delete.`
                                : "Drag across a graph to select a span of keys. Shift-click to add one. Wheel to zoom."}
                        </div>
                    )}

                    <div
                        ref={scrubRef}
                        className={styles.scrub}
                        onMouseDown={(event: React.MouseEvent) => {
                            setScrubbing(true);
                            seekTo(event.clientX);
                        }}
                    >
                        <div className={styles.scrubHead} style={{ left: playhead }} />
                    </div>
                </div>
            </div>
            </ShotDragContext>
        </div>
    );
}
