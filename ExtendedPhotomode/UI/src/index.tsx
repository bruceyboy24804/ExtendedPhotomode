import type { ModRegistrar } from "cs2/modding";
import { OrbitSelectionButton } from "mods/orbit-selection-button";
import { PhotoModePanelPortal } from "mods/photo-mode-panel-portal";
import { PathLibraryPanel } from "mods/path-library-panel";
import { PathToolOptions, PathToolOptionsVisibility } from "mods/path-tool-options";
import { TimelinePanel } from "mods/timeline-panel";
import { ShotSortButton } from "mods/shot-sort-button";
import { initialize, VC } from "vanilla/Components";

const EXTRA_MODULES = [
    {
        path: "game-ui/editor/widgets/fields/number-slider-field.tsx",
        components: ["FloatSliderField", "IntSliderField"],
    },
    {
        path: "game-ui/editor/widgets/fields/toggle-field.tsx",
        components: ["ToggleField"],
    },
    {
        path: "game-ui/common/input/color-picker/color-field/color-field.tsx",
        components: ["ColorField"],
    },
    {
        path: "game-ui/common/input/text/ellipsis-text-input/ellipsis-text-input.tsx",
        components: ["EllipsisTextInput"],
    },
    {
        // Vanilla's own tangent-to-bezier path builder, used by the editor's animation curve field.
        // Reusing it means our timeline draws curves exactly as the game does — the fiddly half of
        // rendering an AnimationCurve is already solved and shipping.
        path: "game-ui/editor/widgets/fields/animation-curve-field/utils/make-curve-path.ts",
        components: ["makeCurvePath"],
    },
    {
        // Vanilla's own way of drawing a Glyph. The glyphs are solid BLACK fills meant to be tinted,
        // and tinting them is not something CSS can do to an <img>: a mask clips the element's
        // background and its image content alike, so the black art survives and you see a black
        // icon. TintedIcon renders no image at all — it is an empty box whose background is the
        // colour and whose mask-image is the glyph, so the shape comes out of the tint.
        path: "game-ui/common/image/tinted-icon.tsx",
        components: ["TintedIcon"],
    },
    {
        // The raw slider, not one of the *SliderField widgets. Those are editor fields: they carry
        // their own label and value column and render as white editor boxes outside the editor.
        // useStepTransformer quantises the drag, which is what keeps a value on whole degrees.
        path: "game-ui/common/input/slider/slider.tsx",
        components: ["Slider", "useStepTransformer"],
    },
];

const EXTRA_THEMES = [
    {
        // The slider's OWN theme, not the slider-field widget's. The field's scss only carries
        // positioning for the settings menu; passing it as `theme` drops the base slider classes
        // and the track loses its height, which stretches the row to tens of thousands of pixels.
        path: "game-ui/common/input/slider/themes/default.module.scss",
        name: "slider",
    },
];

const register: ModRegistrar = (moduleRegistry) => {
    initialize(moduleRegistry, EXTRA_MODULES, EXTRA_THEMES);
    moduleRegistry.extend(
        "game-ui/game/components/photo-mode/photo-mode-panel.tsx",
        "PhotoModePanel",
        (VanillaPanel: React.ComponentType<Record<string, unknown>>) =>
            (props: Record<string, unknown>) => (
                <PhotoModePanelPortal>
                    <VanillaPanel {...props} />
                </PhotoModePanelPortal>
            )
    );

    // The path tool's per-interaction settings go in the game's own tool options panel. The first
    // extend forces that panel visible while our tool runs; the second puts our rows into it.
    moduleRegistry.extend(
        "game-ui/game/components/tool-options/tool-options-panel.tsx",
        "useToolOptionsVisible",
        PathToolOptionsVisibility
    );

    moduleRegistry.extend(
        "game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.tsx",
        "MouseToolOptions",
        PathToolOptions
    );

    moduleRegistry.append("Game", OrbitSelectionButton);

    moduleRegistry.append("Game", PathLibraryPanel);

    // One window. The shot list is a pane inside TimelinePanel rather than a panel of its own.
    moduleRegistry.append("Game", TimelinePanel);

    moduleRegistry.append("Game", ShotSortButton);
};

export default register;
