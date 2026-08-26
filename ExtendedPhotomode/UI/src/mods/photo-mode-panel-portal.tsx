import { type ReactElement, type ReactNode, useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { GenerateShotButton } from "./generate-shot-button";

interface PortalInfo {
    readonly target: Element;
    readonly htmlTemplate: string;
}

function makeHost(): HTMLElement {
    const host = document.createElement("span");

    host.style.flexShrink = "1";
    host.style.minWidth   = "0";

    return host;
}

/**
 * Wraps the vanilla photo mode panel without changing how it renders, borrowing its lifecycle to
 * mount a React portal next to the vanilla "Take Photo" button.
 *
 * The technique is lifted from Hall of Fame's `PhotoModePanelPortal`, which is the reference for
 * adding controls to this panel: the panel exposes no slot for extra buttons, so the only stable
 * way in is to locate the Take Photo button in the DOM and insert a sibling next to it.
 */
export function PhotoModePanelPortal({ children }: { readonly children: ReactNode }): ReactElement {
    const [portalInfo, setPortalInfo] = useState<PortalInfo>();

    useEffect(() => {
        const takePhotoButton = document.querySelector('[style*="TakePicture"]')?.parentElement;

        if (!takePhotoButton) {
            console.warn("ExtendedPhotomode: could not locate the Take Photo button; Generate button not mounted.");
            return;
        }

        const host = makeHost();

        takePhotoButton.insertAdjacentElement("beforebegin", host);

        setPortalInfo({
            target:       host,
            htmlTemplate: takePhotoButton.outerHTML,
        });

        return () => host.remove();
    }, []);

    return (
        <>
            {portalInfo && createPortal(
                <GenerateShotButton html={portalInfo.htmlTemplate} />, portalInfo.target)}
            {children}
        </>
    );
}
