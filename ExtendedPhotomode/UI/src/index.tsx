import type { ModRegistrar } from "cs2/modding";
import { OrbitSelectionButton } from "mods/orbit-selection-button";
import { PhotoModePanelPortal } from "mods/photo-mode-panel-portal";
import { PathLibraryPanel } from "mods/path-library-panel";
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
];

const register: ModRegistrar = (moduleRegistry) => {
    initialize(moduleRegistry, EXTRA_MODULES);
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

    moduleRegistry.append("Game", OrbitSelectionButton);

    moduleRegistry.append("Game", PathLibraryPanel);

    moduleRegistry.append("Game", ShotSortButton);
};

export default register;
