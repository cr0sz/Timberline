public enum ResourceType
{
    Wood,
    Stone,
    // Vestigial. Food was the hunger system's currency; hunger was cut 2026-07-21
    // and nothing produces or consumes Food any more. DO NOT REMOVE OR REORDER —
    // SaveData.resTypes stores these as raw ints, so dropping Food would shift
    // Meat 3->2 and Hide 4->3 and silently rewrite every existing save.
    Food,
    Meat,
    Hide
}
