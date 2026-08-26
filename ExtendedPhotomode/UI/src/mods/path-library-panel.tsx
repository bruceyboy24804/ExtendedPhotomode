import { bindValue, trigger, useValue } from "cs2/api";
import { Button, Scrollable } from "cs2/ui";
import { type ReactElement, useEffect, useState } from "react";
import mod from "mod.json";
import { VC } from "vanilla/Components";
import styles from "./path-library-panel.module.scss";

type PathLibraryEntry = {
    index: number;

    name: string;

    points: number;
};

const savedPaths$ = bindValue<PathLibraryEntry[]>(mod.id, "BINDING:savedPaths", []);

const pathToolActive$ = bindValue<boolean>(mod.id, "BINDING:pathToolActive", false);

const MAX_NAME = 60;

const MIN_SEARCHABLE = 4;

function TextField({
    value,
    placeholder,
    maxLength,
    onChange,
    onBlur,
}: {
    readonly value: string;
    readonly placeholder: string;
    readonly maxLength?: number;
    readonly onChange: (value: string) => void;
    readonly onBlur?: () => void;
}): ReactElement {
    const handle = (event: React.ChangeEvent<HTMLInputElement>): void => onChange(event.target.value);

    return VC.EllipsisTextInput ? (
        <VC.EllipsisTextInput
            value={value}
            placeholder={placeholder}
            maxLength={maxLength}
            onChange={handle}
            onBlur={onBlur}
        />
    ) : (
        <input
            className={styles.field}
            type="text"
            value={value}
            placeholder={placeholder}
            maxLength={maxLength}
            onChange={handle}
            onBlur={onBlur}
        />
    );
}

function savePath(name: string): void {
    trigger("audio", "playSound", "select-item", 1);
    trigger(mod.id, "TRIGGER:savePath", name);
}

function loadPath(id: number): void {
    trigger("audio", "playSound", "select-item", 1);
    trigger(mod.id, "TRIGGER:loadPath", id);
}

function deletePath(id: number): void {
    trigger(mod.id, "TRIGGER:deletePath", id);
}

function renamePath(id: number, name: string): void {
    trigger(mod.id, "TRIGGER:renamePath", id, name);
}

/**
 * The saved path library: the named paths, with save/load/delete controls under them.
 *
 * A React panel rather than more photo mode properties because the photo mode panel renders only a
 * fixed handful of widget types and a list is not one of them — the same wall that turned the
 * per-node editor into a point selector.
 */
export function PathLibraryPanel(): ReactElement | null {
    const paths = useValue(savedPaths$);
    const toolActive = useValue(pathToolActive$);
    const [selected, setSelected] = useState(-1);
    const [name, setName] = useState("");
    const [search, setSearch] = useState("");

    const path = paths.find((entry) => entry.index === selected) ?? null;

    useEffect(() => setName(path?.name ?? ""), [path?.name, path?.index]);

    if (!toolActive) {
        return null;
    }

    const trimmed = name.trim();
    const searchable = paths.length > MIN_SEARCHABLE;
    const term = search.trim().toLowerCase();

    const shown =
        searchable && term ? paths.filter((entry) => entry.name.toLowerCase().includes(term)) : paths;

    const commitName = (): void => {
        if (path && trimmed && trimmed !== path.name) {
            renamePath(path.index, trimmed);
        }
    };

    return (
        <div className={styles.panel}>
            <div className={styles.header}>
                <span className={styles.title}>SAVED PATHS</span>
            </div>

            <div className={styles.content}>
                {searchable && (
                    <div className={styles.search}>
                        <TextField value={search} placeholder="Search paths" onChange={setSearch} />
                    </div>
                )}

                {/* stopPropagation so scrolling the list does not also zoom the camera underneath. */}
                <div onWheel={(event) => event.stopPropagation()}>
                    <Scrollable vertical trackVisibility="scrollable" className={styles.list}>
                        {paths.length === 0 ? (
                            <div className={styles.empty}>Draw a path, name it below and press Save.</div>
                        ) : shown.length === 0 ? (
                            <div className={styles.empty}>No path matches “{search.trim()}”.</div>
                        ) : (
                            shown.map((entry) => (
                                <div
                                    key={entry.index}
                                    className={`${styles.item} ${
                                        entry.index === selected ? styles.itemSelected : ""
                                    }`}
                                    onClick={() => setSelected(entry.index)}
                                    onDoubleClick={() => loadPath(entry.index)}
                                >
                                    <span className={styles.itemLabel}>{entry.name}</span>
                                    <span className={styles.itemPoints}>{entry.points}</span>
                                </div>
                            ))
                        )}
                    </Scrollable>
                </div>

                <TextField
                    value={name}
                    maxLength={MAX_NAME}
                    placeholder="Harbour flythrough"
                    onChange={setName}
                    onBlur={commitName}
                />

                <div className={styles.buttonRow}>
                    {/* Saving under a name that already exists overwrites it, so this is both Save
                        and Save As depending on what is typed — hence one button, not two. */}
                    <Button variant="flat" disabled={!trimmed} onSelect={() => savePath(trimmed)}>
                        Save
                    </Button>

                    <Button variant="flat" disabled={!path} onSelect={() => path && loadPath(path.index)}>
                        Load
                    </Button>

                    <Button
                        variant="flat"
                        disabled={!path}
                        onSelect={() => {
                            if (path) {
                                deletePath(path.index);
                                setSelected(-1);
                            }
                        }}
                    >
                        Delete
                    </Button>
                </div>
            </div>
        </div>
    );
}
