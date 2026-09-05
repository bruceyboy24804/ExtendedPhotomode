import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { Button, Scrollable, Tooltip } from "cs2/ui";
import { type ReactElement, useState } from "react";
import styles from "./sequence-library.module.scss";
import { ShotSortRow } from "./shot-sort-button";
import {
    type CinematicAsset,
    cinematicAssets,
    cloudTargets,
    deleteSequence,
    loadSequence,
    saveSequence,
    selectCloudTarget,
    selectedCloudTarget,
} from "./timeline-bindings";

/**
 * Saving and loading whole cinematic sequences.
 *
 * Straight onto vanilla's own asset database through the `cinematicCamera` bindings — the same save,
 * load and delete its panel calls. So a sequence saved here appears in the game's own Save/Load list
 * and vice versa; this is a second door onto one cupboard, not a parallel store.
 *
 * Assets are addressed by GUID throughout, never by name. Names are not unique and the user can
 * change them, and the path the mod already learned this on was the shot list, where addressing rows
 * by index broke the moment anything was renamed.
 */
/**
 * The badge for one saved sequence: where it is stored, or that it cannot be written to.
 *
 * Vanilla's own rule, copied from its save list rather than invented — cloud target first, falling
 * back to a lock for a read-only local asset, and nothing at all for an ordinary local one. So a
 * read-only Steam sequence shows the Steam glyph, not the lock, exactly as the game's list does.
 */
function assetIcon(cloudTarget: string, readOnly: boolean): string | null {
    switch (cloudTarget) {
        case "ParadoxMods": return "Media/Glyphs/ParadoxModsCloud.svg";
        case "PdxSdk":      return "Media/Glyphs/PDXCloud.svg";
        case "Steamworks":  return "Media/Glyphs/SteamCloud.svg";
        case "Gdk":         return "Media/Glyphs/GdkCloud.svg";
        default:            return readOnly ? "Media/Glyphs/Lock.svg" : null;
    }
}

export function SequenceLibrary(): ReactElement {
    const assets = useValue(cinematicAssets);
    const targets = useValue(cloudTargets);
    const target = useValue(selectedCloudTarget);
    const { translate } = useLocalization();

    // The binding carries the game's internal target ids — "Local", "Steamworks", "PdxSdk" — and the
    // player has never seen those. The game shows the localised name, "Steam Cloud" for Steamworks,
    // and it is worth translating rather than hardcoding so the buttons follow the player's language.
    // Falling back to the raw id keeps an unrecognised platform's button labelled with something.
    const targetName = (id: string): string =>
        translate(`Menu.CLOUD_TARGET[${id}]`, id) ?? id;

    const [selected, setSelected] = useState<string>("");
    const [name, setName] = useState<string>("");

    const chosen = assets.find((asset: CinematicAsset) => asset.guid === selected);

    return (
        <div className={styles.library}>
            {/* The sort control sits with the list it orders. It drives the same order as the button
                in the game's own save/load panel, because both lists are the same assets behind
                vanilla's `assets` binding — so changing it here changes it there too. */}
            <div className={styles.sectionHeader}>
                <span className={styles.sectionLabel}>Saved sequences</span>
                <ShotSortRow />
            </div>

            {/* Local or Steam Cloud, as segmented buttons rather than a dropdown: a <select> kills
                the entire Cohtml UI, which is the first entry in this project's trap list. */}
            {targets.length > 1 && (
                <div className={styles.targets}>
                    {targets.map((option: string) => (
                        <Tooltip
                            key={option}
                            tooltip={`Show sequences stored in ${targetName(option)}.`}
                        >
                            <Button
                                variant="flat"
                                className={
                                    option === target
                                        ? `${styles.target} ${styles.targetOn}`
                                        : styles.target
                                }
                                onSelect={() => selectCloudTarget(option)}
                            >
                                {targetName(option)}
                            </Button>
                        </Tooltip>
                    ))}
                </div>
            )}

            <div onWheel={(event) => event.stopPropagation()}>
                <Scrollable vertical trackVisibility="scrollable" className={styles.list}>
                    {assets.length === 0 && (
                        <div className={styles.empty}>No saved sequences.</div>
                    )}

                    {assets.map((asset: CinematicAsset) => (
                        <div
                            key={asset.guid}
                            className={
                                asset.guid === selected
                                    ? `${styles.item} ${styles.itemSelected}`
                                    : styles.item
                            }
                            onClick={() => {
                                setSelected(asset.guid);

                                // Selecting fills the name box, so Save overwrites what you picked
                                // rather than silently making a second copy under the old text.
                                setName(asset.name);
                            }}
                            onDoubleClick={() => loadSequence(asset.guid, asset.cloudTarget)}
                        >
                            <span className={styles.itemLabel}>{asset.name}</span>

                            {/* Masked rather than an <img>: the Glyphs are solid black fills meant
                                to be tinted, so an img renders them as a black blob on a dark row.
                                Same lesson as the timeline's Trash and Loop buttons. */}
                            {(() => {
                                const icon = assetIcon(asset.cloudTarget, asset.readOnly);

                                return icon ? (
                                    <Tooltip
                                        tooltip={
                                            asset.cloudTarget === "Local"
                                                ? "Read only — this sequence cannot be overwritten."
                                                : `Stored in ${targetName(asset.cloudTarget)}.`
                                        }
                                    >
                                        <div
                                            className={styles.cloudIcon}
                                            style={{ maskImage: `url(${icon})` }}
                                        />
                                    </Tooltip>
                                ) : null;
                            })()}

                            {/* Only where the icon does not already say it — a read-only cloud asset
                                shows its cloud glyph, so nothing else reports that it is locked. */}
                            {asset.readOnly && asset.cloudTarget !== "Local" && (
                                <span className={styles.meta}>read only</span>
                            )}
                        </div>
                    ))}
                </Scrollable>
            </div>

            <input
                className={styles.field}
                value={name}
                placeholder="Sequence name"
                onChange={(event) => setName((event.target as HTMLInputElement).value)}
            />

            <div className={styles.buttonRow}>
                {/* An empty guid saves a new asset; a guid overwrites that one. Overwriting is only
                    offered when the typed name still matches the selected asset — retyping it is how
                    you say "save this as something else". */}
                <Tooltip
                    tooltip={
                        chosen && chosen.name === name.trim()
                            ? `Overwrite "${chosen.name}".`
                            : "Save the timeline as a new sequence."
                    }
                >
                    <Button
                        variant="flat"
                        disabled={name.trim().length === 0}
                        onSelect={() =>
                            saveSequence(
                                name.trim(),
                                chosen && chosen.name === name.trim() && !chosen.readOnly
                                    ? chosen.guid
                                    : "",
                            )
                        }
                    >
                        Save
                    </Button>
                </Tooltip>

                <Tooltip tooltip="Replace the timeline with the selected sequence.">
                    <Button
                        variant="flat"
                        disabled={!chosen}
                        onSelect={() => chosen && loadSequence(chosen.guid, chosen.cloudTarget)}
                    >
                        Load
                    </Button>
                </Tooltip>

                <Tooltip tooltip="Delete the selected sequence. This cannot be undone.">
                    <Button
                        variant="flat"
                        disabled={!chosen || chosen.readOnly}
                        onSelect={() => {
                            if (chosen) {
                                deleteSequence(chosen.guid, chosen.cloudTarget);
                                setSelected("");
                            }
                        }}
                    >
                        Delete
                    </Button>
                </Tooltip>
            </div>
        </div>
    );
}
