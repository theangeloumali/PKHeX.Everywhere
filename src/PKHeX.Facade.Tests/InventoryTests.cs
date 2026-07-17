using AwesomeAssertions;
using PKHeX.Core;
using PKHeX.Facade.Repositories;

namespace PKHeX.Facade.Tests;

public class InventoryTests
{
    [Theory]
    [SupportedSaveFiles]
    public void InventoryRepository_ShouldReturnExpectedItem(string saveFile)
    {
        var game = Game.LoadFrom(saveFile);
        var masterball = ItemRepository.GetItem(1);
        masterball.Should().Be(MasterBall);
    }

    [Theory]
    [SupportedSaveFiles]
    public void Inventories_ShouldContainBallsInventory(string saveFile)
    {
        var game = Game.LoadFrom(saveFile);
        game.Trainer.Inventories.InventoryTypes.Should().Contain("Balls");

        var ballInventory = game.Trainer.Inventories.InventoryItems["Balls"];
        ballInventory.AllSupportedItems.Should().Contain(MasterBall);
    }

    [Theory]
    [SupportedSaveFiles]
    public void Inventories_ShouldAllowChangingItemAmount(string saveFile)
    {
        var game = Game.LoadFrom(saveFile);
        var ballInventory = game.Trainer.Inventories.InventoryItems["Balls"];
        ballInventory.Set(MasterBall.Id, 5);

        game.SaveAndReload(reloadedGame =>
        {
            reloadedGame.Trainer.Inventories.InventoryItems["Balls"].Items
                .Should().ContainSingle(i => i.Id == MasterBall.Id && i.Count == 5);
        });
    }

    [Theory]
    [SupportedSaveFiles]
    public void Inventories_ShouldAllowRemovingItem(string saveFile)
    {
        var game = Game.LoadFrom(saveFile);
        var ballInventory = game.Trainer.Inventories.InventoryItems["Balls"];
        ballInventory.Remove(MasterBall.Id);

        game.SaveAndReload(reloadedGame =>
        {
            reloadedGame.Trainer.Inventories.InventoryItems["Balls"].Items
                .Should().NotContain(i => i.Id == MasterBall.Id);
        });
    }

    [Theory]
    [SupportedSaveFiles]
    public void Invories_CanAddRareCandies(string saveFile)
    {
        var game = Game.LoadFrom(saveFile);

        var rareCandyBag =
            game.Trainer.Inventories.InventoryItems.Values.FirstOrDefault(i =>
                i.AllSupportedItems.Any(s => s.Name == "Rare Candy"));

        rareCandyBag.Should().NotBeNull();

        var rareCandyDefinition = rareCandyBag!.AllSupportedItems.First(s => s.Name == "Rare Candy");
        rareCandyBag.Set(rareCandyDefinition.Id, 5);

        game.SaveAndReload(reloadedGame =>
        {
            reloadedGame.Trainer.Inventories[rareCandyBag.Type].Items
                .Should().ContainSingle(i => i.Id == rareCandyDefinition.Id && i.Count == 5);
        });
    }

    [Theory]
    [SupportedSaveFiles]
    public void Inventories_ShouldFillSupportedPouchesAndPersist(string saveFile)
    {
        var game = Game.LoadFrom(saveFile);
        var originalKeyItems = GetItemsByKeyClassification(game, keyItemsOnly: true);

        var fillSummary = game.Trainer.Inventories.SetAllSupportedToMax();

        fillSummary.RequestedItemCount.Should().BeGreaterThan(0);
        fillSummary.AddedItemCount.Should().BeGreaterThan(0);
        fillSummary.AddedItemCount.Should().BeLessThanOrEqualTo(fillSummary.RequestedItemCount);
        GetItemsByKeyClassification(game, keyItemsOnly: true).Should().Equal(originalKeyItems);

        game.SaveAndReload(reloadedGame =>
        {
            reloadedGame.Trainer.Inventories.InventoryItems.Values
                .SelectMany(inventory => inventory.AllExceptNone())
                .Should().Contain(item => item.Count > 0);
        });
    }

    [Theory]
    [SupportedSaveFiles]
    public void Inventories_ShouldRequest900ForExistingStandardItemStacks(string saveFile)
    {
        const int requestedStandardItemCount = 900;
        var game = Game.LoadFrom(saveFile);
        var bag = game.SaveFile.Inventory;
        var (pouch, itemId) = GetFirstSupportedStandardItem(bag);
        var inventory = game.Trainer.Inventories[pouch.Type.ToString()];
        var expectedCount = bag.Clamp(pouch.Type, itemId, requestedStandardItemCount);

        inventory.Set(itemId, 99);
        inventory.Items.Should().ContainSingle(item => item.Id == itemId && item.Count == 99);

        var fillSummary = game.Trainer.Inventories.SetAllSupportedToMax();
        var actualCount = game.Trainer.Inventories[pouch.Type.ToString()].Items
            .Single(item => item.Id == itemId)
            .Count;

        actualCount.Should().Be(expectedCount);
        if (expectedCount == requestedStandardItemCount)
            actualCount.Should().Be(requestedStandardItemCount);
        else
            fillSummary.ClampedItemCount.Should().BeGreaterThan(0);
    }

    [Theory]
    [SupportedSaveFiles]
    public void Inventories_ShouldFillOnlySupportedKeyItems(string saveFile)
    {
        var game = Game.LoadFrom(saveFile);
        var originalNonKeyItems = GetItemsByKeyClassification(game, keyItemsOnly: false);

        var fillSummary = game.Trainer.Inventories.SetSupportedKeyItemsToMax();

        fillSummary.RequestedItemCount.Should().BeGreaterThanOrEqualTo(0);
        if (fillSummary.RequestedItemCount > 0)
            fillSummary.AddedItemCount.Should().BeGreaterThan(0);

        GetItemsByKeyClassification(game, keyItemsOnly: false).Should().Equal(originalNonKeyItems);
    }

    private static (InventoryType Type, int Id, int Count)[] GetItemsByKeyClassification(
        Game game,
        bool keyItemsOnly)
    {
        var bag = game.SaveFile.Inventory;

        return bag.Pouches
            .SelectMany(pouch => pouch.Items.Select(item => (pouch.Type, Id: item.Index, item.Count)))
            .Where(item => item.Id != 0)
            .Where(item => Inventories.IsKeyItem(bag.Info, item.Type, (ushort)item.Id) == keyItemsOnly)
            .ToArray();
    }

    private static (InventoryPouch Pouch, ushort ItemId) GetFirstSupportedStandardItem(PlayerBag bag)
    {
        foreach (var pouch in bag.Pouches)
        {
            var itemId = pouch.GetAllItems()
                .ToArray()
                .FirstOrDefault(candidate =>
                    !Inventories.IsKeyItem(bag.Info, pouch.Type, candidate) &&
                    bag.IsLegal(pouch.Type, candidate, 900));

            if (itemId != ItemDefinition.None)
                return (pouch, itemId);
        }

        throw new InvalidOperationException("No supported standard item is available.");
    }
}
