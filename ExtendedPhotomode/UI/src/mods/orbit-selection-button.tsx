import { trigger } from "cs2/api";
import { selectedInfo } from "cs2/bindings";
import { Tooltip } from "cs2/ui";
import { type ReactElement, useEffect } from "react";
import ReactDOM from "react-dom";
import mod from "mod.json";

function orbitSelection(): void {
    trigger("audio", "playSound", "select-item", 1);
    trigger(mod.id, "TRIGGER:orbitSelection");
}

const kActionsSelector = ".actions-section_X1x";

const kOutOfServiceSelector = ".out-of-service_Kfh";

const kInjectedClass = "epm-injected-div";

const kIconPath = "coui://extendedphotomode/Camera_Icons/Pin.svg";

const kIntervalKey = "epmOrbitObserverInterval";

function OrbitInfoButton(): ReactElement {
    return (
        <Tooltip tooltip="Centre the photo mode orbit on this object">
            <button
                style={{ marginLeft: "6rem", marginRight: "8rem" }}
                className="ok button_Z9O button_ECf item_It6 item-mouse-states_Fmi item-selected_tAM item-focused_FuT button_Z9O button_ECf item_It6 item-mouse-states_Fmi item-selected_tAM item-focused_FuT button_xGY"
                onClick={orbitSelection}
            >
                <img className="icon_Tdt icon_soN icon_Iwk" src={kIconPath} />
            </button>
        </Tooltip>
    );
}

/**
 * Injects the orbit button into the selected object's info panel.
 *
 * Built the same way First Person Camera Continued builds its follow button, because that is proven
 * to work in this game: the info panel is rebuilt on every selection change and offers no slot for
 * extra buttons, so the actions row is polled for after each change and the button rendered into a
 * marker div placed before the on/off control. The polling handle lives on `window` and is cleared
 * both on success and by a deadline, so a selection with no actions row cannot leave one running.
 */
export function OrbitSelectionButton(): ReactElement | null {
    useEffect(() => {
        const clearPolling = (): void => {
            const handle = (window as unknown as Record<string, number | undefined>)[kIntervalKey];

            if (handle !== undefined) {
                clearInterval(handle);
                delete (window as unknown as Record<string, number | undefined>)[kIntervalKey];
            }
        };

        const checkAndInject = (): void => {
            const actions = document.querySelector(kActionsSelector);

            if (!actions || actions.querySelector(`div.${kInjectedClass}`)) {
                return;
            }

            const div = document.createElement("div");
            div.className = kInjectedClass;
            ReactDOM.render(<OrbitInfoButton />, div);

            const outOfService = actions.querySelector(kOutOfServiceSelector);
            if (outOfService) {
                actions.insertBefore(div, outOfService);
            } else {
                actions.appendChild(div);
            }

            clearPolling();
        };

        const observeAndAppend = (): void => {
            clearPolling();
            checkAndInject();

            (window as unknown as Record<string, number>)[kIntervalKey] =
                setInterval(checkAndInject, 100) as unknown as number;

            setTimeout(clearPolling, 5000);
        };

        const subscription = selectedInfo.selectedEntity$.subscribe(() => observeAndAppend());

        return () => {
            clearPolling();
            subscription.dispose();
        };
    }, []);

    return null;
}
