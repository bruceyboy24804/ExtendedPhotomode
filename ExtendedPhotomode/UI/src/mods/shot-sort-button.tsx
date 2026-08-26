import { bindValue, trigger, useValue } from "cs2/api";
import { Tooltip } from "cs2/ui";
import { type ReactElement, useEffect, useState } from "react";
import ReactDOM from "react-dom";
import mod from "mod.json";
import styles from "./shot-sort-button.module.scss";

const sortOrder$ = bindValue<number>(mod.id, "BINDING:shotSortOrder", 1);

const kButtonRowSelector = ".button-row_odR";

const kInjectedClass = "epm-sort-injected";

const kIntervalKey = "epmShotSortInterval";
const ArrowSortUp   = "coui://uil/Standard/ArrowSortLowDown.svg";
const ArrowSortDown = "coui://uil/Standard/ArrowSortHighDown.svg";
const IconNameSort  = "coui://uil/Standard/NameSort.svg";
const IconClock     = "coui://uil/Standard/Clock.svg";
const IconListView  = "coui://uil/Standard/ListView.svg";

const ArrowSortAZ = "coui://extendedphotomode/Sorting/EPM_Sorting_AZ.svg";
const ArrowSortZA = "coui://extendedphotomode/Sorting/EPM_Sorting_ZA.svg";

const kTriggerIcon = "Media/Glyphs/More.svg";
const Order = {
    Default:        0,
    NameAscending:  1,
    NameDescending: 2,
    Newest:         3,
    Oldest:         4,
} as const;

type SortKey = "game" | "name" | "date";

function keyOf(order: number): SortKey {
    if (order === Order.NameAscending || order === Order.NameDescending) {
        return "name";
    }

    return order === Order.Newest || order === Order.Oldest ? "date" : "game";
}

function isAscending(order: number): boolean {
    return order === Order.NameAscending || order === Order.Oldest;
}

function orderFrom(key: SortKey, ascending: boolean): number {
    if (key === "game") {
        return Order.Default;
    }

    if (key === "name") {
        return ascending ? Order.NameAscending : Order.NameDescending;
    }

    return ascending ? Order.Oldest : Order.Newest;
}

function setOrder(order: number): void {
    trigger("audio", "playSound", "select-item", 1);
    trigger(mod.id, "TRIGGER:setShotSortOrder", order);
}

function Option({
    icon,
    tooltip,
    selected,
    disabled,
    onSelect,
}: {
    readonly icon: string;
    readonly tooltip: string;
    readonly selected: boolean;
    readonly disabled?: boolean;
    readonly onSelect: () => void;
}): ReactElement {
    return (
        <Tooltip tooltip={tooltip}>
            <button
                className={`${styles.option} ${selected ? styles.optionSelected : ""}`}
                disabled={disabled}
                onClick={disabled ? undefined : onSelect}
            >
                {/* Applied as a mask rather than an <img>, which is how the game tints its own icons:
                    it makes these follow the text colour and grey out with the button. Anything that
                    renders as a solid blob is a filled SVG, since a mask only reads alpha. */}
                <div className={styles.icon} style={{ maskImage: `url(${icon})` }} />
            </button>
        </Tooltip>
    );
}

function ShotSortControl(): ReactElement {
    const order = useValue(sortOrder$);
    const [open, setOpen] = useState(false);

    const key       = keyOf(order);
    const ascending = isAscending(order);

    return (
        <>
            {open && (
                <div className={styles.popup}>
                    <div className={styles.title}>SORTING</div>

                    <div className={styles.row}>
                        <span className={styles.label}>Sorting</span>
                        <div className={styles.options}>
                            <Option icon={IconListView} tooltip="Leave the game's own order alone"
                                    selected={key === "game"}
                                    onSelect={() => setOrder(orderFrom("game", ascending))} />
                            <Option icon={IconNameSort} tooltip="Sort by name"
                                    selected={key === "name"}
                                    onSelect={() => setOrder(orderFrom("name", ascending))} />
                            <Option icon={IconClock} tooltip="Sort by when the shot was last saved"
                                    selected={key === "date"}
                                    onSelect={() => setOrder(orderFrom("date", ascending))} />
                        </div>
                    </div>

                    <div className={styles.row}>
                        <span className={styles.label}>Order</span>
                        <div className={styles.options}>
                            {/* The direction icons follow the sort key, because "ascending" means
                                different things: A to Z for a name, oldest first for a date. Disabled
                                rather than hidden under Game order — a control that vanishes is harder
                                to understand than one visibly not applicable. */}
                            <Option icon={key === "date" ? ArrowSortUp : ArrowSortAZ}
                                    tooltip={key === "date" ? "Oldest first" : "A to Z"}
                                    selected={key !== "game" && ascending}
                                    disabled={key === "game"}
                                    onSelect={() => setOrder(orderFrom(key, true))} />
                            <Option icon={key === "date" ? ArrowSortDown : ArrowSortZA}
                                    tooltip={key === "date" ? "Newest first" : "Z to A"}
                                    selected={key !== "game" && !ascending}
                                    disabled={key === "game"}
                                    onSelect={() => setOrder(orderFrom(key, false))} />
                        </div>
                    </div>
                </div>
            )}

            <Tooltip tooltip="Choose how your saved shots are ordered">
                {/* Vanilla's own class list from the Save button beside it, copied verbatim including
                    the doubled names — those are generated and carry the sizing. */}
                <button
                    className="button_Ytr item-states_QjV button_Ytr item-states_QjV"
                    onClick={() => setOpen(!open)}
                >
                    <div
                        className="tinted-icon_iKo icon_PhD"
                        style={{ maskImage: `url(${kTriggerIcon})` }}
                    />
                </button>
            </Tooltip>
        </>
    );
}

/**
 * Injects the sorting button into the save/load panel's button row.
 *
 * The panel is created and destroyed as it is opened and closed and offers no slot for extra
 * controls, so the row is polled for — the same approach the orbit selection button uses against the
 * info panel, which is proven to work here. The handle lives on `window` and is cleared both on
 * success and by a deadline, so a panel that never opens cannot leave one running.
 */
export function ShotSortButton(): ReactElement | null {
    useEffect(() => {
        const clearPolling = (): void => {
            const handle = (window as unknown as Record<string, number | undefined>)[kIntervalKey];

            if (typeof handle === "number") {
                window.clearInterval(handle);
                delete (window as unknown as Record<string, number | undefined>)[kIntervalKey];
            }
        };

        clearPolling();

        const started = Date.now();

        const handle = window.setInterval(() => {
            if (Date.now() - started > 60000) {
                clearPolling();
                return;
            }

            const row = document.querySelector(kButtonRowSelector);

            if (!row || row.querySelector(`.${kInjectedClass}`)) {
                return;
            }

            const host = document.createElement("span");
            host.className = kInjectedClass;

            host.style.position = "relative";

            row.appendChild(host);

            ReactDOM.render(<ShotSortControl />, host);
        }, 500);

        (window as unknown as Record<string, number | undefined>)[kIntervalKey] = handle;

        return clearPolling;
    }, []);

    return null;
}
