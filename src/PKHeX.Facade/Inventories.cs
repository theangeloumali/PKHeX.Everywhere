using System.Collections.Immutable;
using PKHeX.Core;

namespace PKHeX.Facade;

public class Inventories
{
    private readonly Game _game;

    public Inventories(Game game)
    {
        _game = game;

        InventoryTypes = GetInventoryTypes();
        InventoryItems = GetInventories();
    }

    public Inventory this[string key]
    {
        get
        {
            if (InventoryItems.ContainsKey(key))
            {
                return InventoryItems[key];
            }
            else
            {
                throw new KeyNotFoundException($"The inventory '{key}' does not exist.");
            }
        }
    }


    public ImmutableHashSet<string> InventoryTypes { get; private set; }
    public ImmutableDictionary<string, Inventory> InventoryItems { get; private set; }
    public bool HasSupportedKeyItems
    {
        get
        {
            var bag = _game.SaveFile.Inventory;
            return bag.Pouches.Any(pouch =>
                pouch.GetAllItems().ToArray().Any(itemId => IsKeyItem(bag.Info, pouch.Type, itemId)));
        }
    }

    public InventoryFillSummary SetAllSupportedToMax()
        => SetSupportedItemsToMax(keyItemsOnly: false);

    public InventoryFillSummary SetSupportedKeyItemsToMax()
        => SetSupportedItemsToMax(keyItemsOnly: true);

    private InventoryFillSummary SetSupportedItemsToMax(bool keyItemsOnly)
    {
        var bag = _game.SaveFile.Inventory;
        var requestedItemCount = 0;
        var addedItemCount = 0;

        foreach (var pouch in bag.Pouches)
        {
            var legalItemIds = pouch.GetAllItems()
                .ToArray()
                .Where(itemId => IsKeyItem(bag.Info, pouch.Type, itemId) == keyItemsOnly)
                .Where(itemId => bag.IsLegal(pouch.Type, itemId, pouch.MaxCount))
                .ToArray();
            requestedItemCount += legalItemIds.Length;
            foreach (var legalItemId in legalItemIds)
                pouch.GiveItem(bag, legalItemId, pouch.MaxCount);

            addedItemCount += legalItemIds.Count(itemId =>
                pouch.Items.Any(item => item.Index == itemId && item.Count > 0));
        }

        bag.CopyTo(_game.SaveFile);
        InventoryTypes = GetInventoryTypes();
        InventoryItems = GetInventories();

        return new InventoryFillSummary(requestedItemCount, addedItemCount);
    }

    internal static bool IsKeyItem(IItemStorage itemStorage, InventoryType pouchType, ushort itemId)
    {
        if (pouchType is InventoryType.KeyItems)
            return true;

        return itemStorage switch
        {
            ItemStorage7GG => ItemStorage7GG.Key.Contains(itemId),
            ItemStorage8BDSP => ItemStorage8BDSP.GetInventoryPouch(itemId) is InventoryType.KeyItems,
            ItemStorage9SV => ItemStorage9SV.GetInventoryPouch(itemId) is InventoryType.KeyItems,
            ItemStorage9ZA => ItemStorage9ZA.GetInventoryPouch(itemId) is InventoryType.KeyItems,
            _ => false,
        };
    }

    private ImmutableHashSet<string> GetInventoryTypes()
        => _game.SaveFile.Inventory.Pouches.Select(i => i.Type.ToString()).ToImmutableHashSet();

    private ImmutableDictionary<string, Inventory> GetInventories() => InventoryTypes.ToImmutableDictionary(
        type => type,
        type => new Inventory(type, _game)
    );
}

public static class InventoriesExtensions
{
    public static IEnumerable<Inventory.Item> AllExceptNone(this Inventory inventory)
    {
        return inventory.Where(i => !i.IsNone);
    }
}

public readonly record struct InventoryFillSummary(int RequestedItemCount, int AddedItemCount)
{
    public int SkippedItemCount => RequestedItemCount - AddedItemCount;
}
