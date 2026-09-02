namespace NetLoom.Application.Inventory
{
    public interface IInventoryCollector
    {
        InventorySnapshot Collect(InventoryCollectionRequest request);
    }
}
