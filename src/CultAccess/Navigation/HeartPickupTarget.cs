using CultAccess.Util;

namespace CultAccess.Navigation
{
    /// <summary>
    /// Restores the semantic inventory name hidden behind the heart pickups' icon-only
    /// interaction label. The game deliberately publishes "." for these interactions.
    /// </summary>
    internal static class HeartPickupTarget
    {
        internal static string Name(Interaction_HeartPickupBase pickup)
        {
            if (pickup == null) return "Heart pickup";

            if (!TryItemType(pickup, out var itemType))
                return "Heart pickup";

            var term = $"Inventory/{itemType}";
            try
            {
                var localized = InventoryItem.LocalizedName(itemType);
                if (RichText.IsUsableLocalization(localized, term) &&
                    RichText.HasSemanticText(localized))
                    return RichText.Clean(localized);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning(
                    $"Heart pickup localisation failed for {itemType}: {e.Message}");
            }

            return FallbackName(itemType);
        }

        private static bool TryItemType(
            Interaction_HeartPickupBase pickup,
            out InventoryItem.ITEM_TYPE itemType)
        {
            if (pickup is Interaction_RedHeart red)
            {
                itemType = red.HP >= 2
                    ? InventoryItem.ITEM_TYPE.RED_HEART
                    : InventoryItem.ITEM_TYPE.HALF_HEART;
                return true;
            }

            if (pickup is Interaction_BlueHeart blue)
            {
                itemType = blue.HP >= 2
                    ? InventoryItem.ITEM_TYPE.BLUE_HEART
                    : InventoryItem.ITEM_TYPE.HALF_BLUE_HEART;
                return true;
            }

            if (pickup is Interaction_BlackHeart)
            {
                itemType = InventoryItem.ITEM_TYPE.BLACK_HEART;
                return true;
            }

            if (pickup is Interaction_FireHeart)
            {
                itemType = InventoryItem.ITEM_TYPE.FIRE_HEART;
                return true;
            }

            if (pickup is Interaction_IceHeart)
            {
                itemType = InventoryItem.ITEM_TYPE.ICE_HEART;
                return true;
            }

            itemType = default;
            return false;
        }

        private static string FallbackName(InventoryItem.ITEM_TYPE itemType)
        {
            switch (itemType)
            {
                case InventoryItem.ITEM_TYPE.RED_HEART: return "Red Heart";
                case InventoryItem.ITEM_TYPE.HALF_HEART: return "Half Red Heart";
                case InventoryItem.ITEM_TYPE.BLUE_HEART: return "Blue Heart";
                case InventoryItem.ITEM_TYPE.HALF_BLUE_HEART: return "Half Blue Heart";
                case InventoryItem.ITEM_TYPE.BLACK_HEART: return "Black Heart";
                case InventoryItem.ITEM_TYPE.FIRE_HEART: return "Fire Heart";
                case InventoryItem.ITEM_TYPE.ICE_HEART: return "Ice Heart";
                default: return "Heart pickup";
            }
        }
    }
}
