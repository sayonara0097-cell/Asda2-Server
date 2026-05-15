using System.Collections.Generic;
using System.Linq;
using WCell.Constants.Items;
using WCell.RealmServer.Database;
using WCell.RealmServer.Entities;
using WCell.RealmServer.Items;

namespace WCell.RealmServer.Asda2Quests
{
    internal static class Asda2QuestInventoryHelper
    {
        private sealed class AddedItem
        {
            public AddedItem(Asda2Item item, int amount)
            {
                Item = item;
                Amount = amount;
            }

            public readonly Asda2Item Item;
            public readonly int Amount;
        }

        public static bool TryAddItems(Character chr, int[] itemIds, int[] amounts)
        {
            if (chr == null || itemIds == null || amounts == null)
                return false;

            var addedItems = new List<AddedItem>();
            var count = itemIds.Length < amounts.Length ? itemIds.Length : amounts.Length;
            for (var index = 0; index < count; index++)
            {
                if (!TryAddItem(chr, itemIds[index], amounts[index], addedItems))
                {
                    RollbackAddedItems(addedItems);
                    return false;
                }
            }

            return true;
        }

        public static bool TryAddStartItems(Character chr, int[] itemIds)
        {
            if (chr == null || itemIds == null)
                return false;

            var addedItems = new List<AddedItem>();
            for (var index = 0; index < itemIds.Length; index++)
            {
                if (!TryAddItem(chr, itemIds[index], 1, addedItems))
                {
                    RollbackAddedItems(addedItems);
                    return false;
                }
            }

            return true;
        }

        public static void RemoveQuestItems(Character chr, Asda2QuestProgressRecord quest,
            Asda2QuestTemplateInfo template)
        {
            if (chr == null || quest == null)
                return;

            for (var index = 1; index <= 5; index++)
                RemoveItemAmount(chr, GetQuestItemId(quest, index), GetTurnInAmount(quest, template, index));
        }

        public static void RemoveItemAmount(Character chr, int itemId, int amount)
        {
            if (chr == null || itemId <= 0 || amount <= 0)
                return;

            var remaining = amount;
            var items = chr.Asda2Inventory.RegularItems.Concat(chr.Asda2Inventory.ShopItems)
                .Where(item => item != null && item.Template != null && item.Template.Id == itemId && item.Amount > 0)
                .ToArray();

            foreach (var item in items)
            {
                var removeAmount = remaining < item.Amount ? remaining : item.Amount;
                item.ModAmount(-removeAmount);
                remaining -= removeAmount;
                if (remaining <= 0)
                    return;
            }
        }

        private static bool TryAddItem(Character chr, int itemId, int amount, ICollection<AddedItem> addedItems)
        {
            if (itemId <= 0 || amount <= 0)
                return true;

            if (Asda2ItemMgr.GetTemplate(itemId) == null)
                return false;

            Asda2Item item = null;
            if (chr.Asda2Inventory.TryAdd(itemId, amount, true, ref item, null, null) != Asda2InventoryError.Ok ||
                item == null)
                return false;

            addedItems.Add(new AddedItem(item, amount));
            return true;
        }

        private static void RollbackAddedItems(IEnumerable<AddedItem> addedItems)
        {
            foreach (var addedItem in addedItems.Reverse())
            {
                if (addedItem.Item != null && !addedItem.Item.IsDeleted)
                    addedItem.Item.ModAmount(-addedItem.Amount);
            }
        }

        private static int GetQuestItemId(Asda2QuestProgressRecord quest, int objectiveIndex)
        {
            switch (objectiveIndex)
            {
                case 1:
                    return quest.Item1Id;
                case 2:
                    return quest.Item2Id;
                case 3:
                    return quest.Item3Id;
                case 4:
                    return quest.Item4Id;
                case 5:
                    return quest.Item5Id;
                default:
                    return -1;
            }
        }

        private static int GetTurnInAmount(Asda2QuestProgressRecord quest, Asda2QuestTemplateInfo template,
            int objectiveIndex)
        {
            var requiredAmount = template == null || objectiveIndex < 1 || objectiveIndex > 5
                ? 0
                : template.RequiredAmounts[objectiveIndex - 1];
            return requiredAmount > 0 ? requiredAmount : GetQuestObjectiveAmount(quest, objectiveIndex);
        }

        private static int GetQuestObjectiveAmount(Asda2QuestProgressRecord quest, int objectiveIndex)
        {
            switch (objectiveIndex)
            {
                case 1:
                    return quest.Item1Amount;
                case 2:
                    return quest.Item2Amount;
                case 3:
                    return quest.Item3Amount;
                case 4:
                    return quest.Item4Amount;
                case 5:
                    return quest.Item5Amount;
                default:
                    return 0;
            }
        }
    }
}
