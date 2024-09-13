using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using System.Linq;
using System;
using StardewValley.ItemTypeDefinitions;
using StardewValley.GameData.FishPonds;
using Microsoft.Xna.Framework.Content;

namespace FilteredChestHopper
{
    internal class Pipeline
    {
        public List<Chest> Hoppers = new List<Chest>();
        //location
        internal GameLocation Location;

        public Pipeline(Chest originHopper)
        {
            Location = originHopper.Location;

            originHopper.modData[Mod.ModDataFlag] = "1";

            Hoppers.Add(originHopper);

            CheckSideHoppers(new Vector2(1, 0), originHopper);
            CheckSideHoppers(new Vector2(-1, 0), originHopper);

            Hoppers.Sort(new ChestLeftToRight());
        }

        //Checks adjacent hoppers for expansion
        private void CheckSideHoppers(Vector2 direction, Chest hopper)
        {
            //check for hopper in direction
            Chest chest = Mod.GetChestAt(Location, hopper.TileLocation + direction);
            if (chest == null || !Mod.TryGetHopper(chest, out hopper))
            {
                return;
            }

            ExpandPipeline(hopper);

            CheckSideHoppers(direction, hopper);
        }

        internal void ExpandPipeline(Chest hopper)
        {
            //Expand Pipeline
            Hoppers.Add(hopper);
            hopper.modData[Mod.ModDataFlag] = "1";
        }

        //Attempt to output with this hopper as a filter
        public void AttemptTransfer(Mod mod)
        {
            List<Chest> inputChests = new List<Chest>();
            List<Chest[]> outputChests = new List<Chest[]>();
            for (int i = 0; i < Hoppers.Count; i++)
            {
                Chest inputChest = Mod.GetChestAt(Location, Hoppers[i].TileLocation - new Vector2(0,1));
                if (inputChest != null)
                {
                    inputChests.Add(inputChest);
                }

                Chest outputChest = Mod.GetChestAt(Location, Hoppers[i].TileLocation + new Vector2(0, 1));
                if (outputChest != null)
                {
                    outputChests.Add(new Chest[] { Hoppers[i], outputChest});
                }
            }

            foreach (var inputChest in inputChests)
            {
                inputChest.clearNulls();
                var chestAboveItems = inputChest.GetItemsForPlayer(inputChest.owner.Value);
                foreach (var outputChest in outputChests)
                {
                    var filterItems = outputChest[0].GetItemsForPlayer(inputChest.owner.Value);
                    for (int i = chestAboveItems.Count - 1; i >= 0; i--)
                    {
                        bool match = true;
                        int filterCount = 0;
                        filterItems.RemoveEmptySlots();
                        for (int j = filterItems.Count - 1; j >= 0; j--)
                        {
                            if(filterItems[j] != null && chestAboveItems[i] != null && filterItems[j].QualifiedItemId == chestAboveItems[i].QualifiedItemId && GetItemsFlavourID(filterItems[j]) == GetItemsFlavourID(chestAboveItems[i]) && (!mod.Config.CompareQuality || filterItems[j].Quality == chestAboveItems[i].Quality))
                            {
                                //Should be able to hanlde all item types now
                                if(mod.Config.CompareQuantity)// && chestAboveItems[i].TypeDefinitionId == ItemRegistry.type_object)
                                {
                                    filterCount = filterItems[j].Stack == 1 ? 0 : filterItems[j].Stack;
                                }
                                match = true;
                                break;
                            }
                            else
                            {
                                match = false;
                            }
                        }
                        if (match)
                        {
                            Item item = chestAboveItems[i];

                            if(filterCount > 0)
                            {
                                int amountToMove = filterCount;

                                //Calculate the amount to move to match the filter amount
                                foreach (var itemStack in outputChest[1].GetItemsForPlayer(inputChest.owner.Value))
                                {
                                    if (itemStack != null && itemStack.canStackWith(item))
                                    {
                                        //handle single items
                                        if (itemStack.Stack == 0)
                                        {
                                            amountToMove--;
                                        }
                                        amountToMove -= itemStack.Stack;
                                    }
                                }

                                //continue if the amount to move is already met
                                if (amountToMove < 1)
                                    continue;

                                StardewValley.Object processedItem = null;
                                string processedItemID = GetItemsFlavourID(item);
                                if(!string.IsNullOrEmpty(processedItemID))
                                {
                                    processedItem = new StardewValley.Object(processedItemID,1);
                                }

                                //Make a new item stack
                                Item newItem = null;
                                //Handle varient items like wine
                                if(processedItem != null)
                                {
                                    ObjectDataDefinition objectDataDefinition = (ObjectDataDefinition)ItemRegistry.GetTypeDefinition(ItemRegistry.type_object);
                                    newItem = GetFlavoredObjectVariant(objectDataDefinition, item as StardewValley.Object, processedItem, ItemRegistry.ItemTypes.Find((potentailItem) => potentailItem.Identifier == item.TypeDefinitionId)).CreateItem();
                                    newItem.Stack = item.Stack;
                                    newItem.Quality = item.Quality;
                                }
                                else    
                                {
                                    newItem = ItemRegistry.Create(item.QualifiedItemId, item.Stack, item.Quality);
                                }

                                //Limit the new stack size to the amount to Move
                                if (newItem.Stack > amountToMove)
                                {
                                    newItem.Stack = amountToMove;
                                }

                                //Attempt the addition
                                if (outputChest[1].addItem(newItem) == null)
                                {
                                    //clean up the old stack
                                    if (item.Stack == newItem.Stack)
                                    {
                                        chestAboveItems.RemoveAt(i);
                                    }
                                    //handle single items
                                    else if(newItem.Stack == 0)
                                    {
                                        item.Stack--;
                                    }
                                    //or just remove larger stacks
                                    else
                                    {
                                        item.Stack -= newItem.Stack;
                                    }
                                }
                            }
                            else
                            {
                                //The Easy Way
                                if (outputChest[1].addItem(item) == null)
                                {
                                    chestAboveItems.RemoveAt(i);
                                }
                            }
                        }
                    }
                }
            }
        }

        public class ChestLeftToRight : Comparer<Chest>
        {
            public override int Compare(Chest x, Chest y)
            {
                return x.TileLocation.X.CompareTo(y.TileLocation.X);
            }
        }

        public string GetItemsFlavourID(Item item)
        {
            foreach (string contextTag in item.GetContextTags())
            {
                if(contextTag.Contains("preserve_sheet_index_"))
                {
                    return contextTag.Replace("preserve_sheet_index_", ""); 
                }
            }
            return null;
        }

        //Everything past this I adapted (stole) from CJB Item Spawner, thanks CJB

        /// <summary>Get flavored variants of a base item (like Blueberry Wine for Blueberry), if any.</summary>
        private FilteredItem GetFlavoredObjectVariant(ObjectDataDefinition objectDataDefinition, StardewValley.Object newItem, StardewValley.Object processedItem, IItemDataDefinition itemType)
        {
            string id = processedItem.ItemId;

            switch(newItem.itemId.Value)
            {
                case "348":
                    return this.TryCreate(itemType.Identifier, $"348/{id}", _ => objectDataDefinition.CreateFlavoredWine(processedItem));
                case "344":
                    return this.TryCreate(itemType.Identifier, $"344/{id}", _ => objectDataDefinition.CreateFlavoredJelly(processedItem));
                case "350":
                    return this.TryCreate(itemType.Identifier, $"350/{id}", _ => objectDataDefinition.CreateFlavoredJuice(processedItem));
                case "342":
                    return this.TryCreate(itemType.Identifier, $"342/{id}", _ => objectDataDefinition.CreateFlavoredPickle(processedItem));
                case "340":
                    return this.TryCreate(itemType.Identifier, $"340/{id}", _ => objectDataDefinition.CreateFlavoredHoney(processedItem));
                case "812":
                    return this.TryCreate(itemType.Identifier, $"812/{id}", _ => objectDataDefinition.CreateFlavoredRoe(processedItem));
                case "447":
                    return this.TryCreate(itemType.Identifier, $"447/{id}", _ => objectDataDefinition.CreateFlavoredAgedRoe(processedItem));
            }
            return null;
        }


        /// <summary>Create a searchable item if valid.</summary>
        /// <param name="type">The item type.</param>
        /// <param name="key">The locally unique item key.</param>
        /// <param name="createItem">Create an item instance.</param>
        private FilteredItem TryCreate(string type, string key, Func<FilteredItem, Item> createItem)
        {
            try
            {
                FilteredItem item = new FilteredItem(type, key, createItem);
                item.Item.getDescription(); // force-load item data, so it crashes here if it's invalid

                if (item.Item.Name is null or "Error Item")
                    return null;

                return item;
            }
            catch
            {
                return null; // if some item data is invalid, just don't include it
            }
        }
    }
}
