﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI;
using System.IO;
using JsonAssets.Data;
using StardewModdingAPI.Events;
using System.Reflection;
using StardewValley;
using Microsoft.Xna.Framework;
using StardewValley.Menus;

namespace JsonAssets
{
    public class Mod : StardewModdingAPI.Mod
    {
        public static Mod instance;

        public override void Entry(IModHelper helper)
        {
            instance = this;

            MenuEvents.MenuChanged += menuChanged;
            SaveEvents.AfterLoad += afterLoad;

            Log.info("Checking content packs...");
            foreach (var dir in Directory.EnumerateDirectories(Path.Combine(Helper.DirectoryPath, "ContentPacks")))
            {
                if (!File.Exists(Path.Combine(dir, "content-pack.json")))
                    continue;
                var packInfo = Helper.ReadJsonFile<ContentPackData>(Path.Combine(dir, "content-pack.json"));
                Log.info($"\t{packInfo.Name} {packInfo.Version} by {packInfo.Author} - {packInfo.Description}");

                if (Directory.Exists(Path.Combine(dir, "Objects")))
                {
                    foreach (var objDir in Directory.EnumerateDirectories(Path.Combine(dir, "Objects")))
                    {
                        if (!File.Exists(Path.Combine(objDir, "object.json")))
                            continue;
                        var objInfo = Helper.ReadJsonFile<ObjectData>(Path.Combine(objDir, "object.json"));
                        objInfo.directory = Path.Combine("ContentPacks", Path.GetFileName(dir), "Objects", Path.GetFileName(objDir));
                        objects.Add(objInfo);
                    }
                }
                if (Directory.Exists(Path.Combine(dir, "Crops")))
                {
                    foreach (var cropDir in Directory.EnumerateDirectories(Path.Combine(dir, "Crops")))
                    {
                        if (!File.Exists(Path.Combine(cropDir, "crop.json")))
                            continue;
                        var cropInfo = Helper.ReadJsonFile<CropData>(Path.Combine(cropDir, "crop.json"));
                        cropInfo.directory = Path.Combine("ContentPacks", Path.GetFileName(dir), "Crops", Path.GetFileName(cropDir));
                        crops.Add(cropInfo);

                        var obj = new ObjectData();
                        obj.directory = cropInfo.directory;
                        obj.imageName = "seeds.png";
                        obj.Name = cropInfo.SeedName;
                        obj.Description = cropInfo.SeedDescription;
                        obj.Category = ObjectData.Category_.Seeds;
                        obj.Price = cropInfo.SeedPurchasePrice;

                        obj.CanPurchase = true;
                        obj.PurchaseFrom = cropInfo.SeedPurchaseFrom;
                        obj.PurchasePrice = cropInfo.SeedPurchasePrice;
                        List<string> seasons = new List<string>();
                        seasons.Add("spring");
                        seasons.Add("summer");
                        seasons.Add("fall");
                        seasons.Add("winter");
                        foreach (var season in cropInfo.Seasons)
                            seasons.Remove(season);
                        var str = "z";
                        foreach (var season in seasons) ;
                            str += " " + seasons;
                        obj.PurchaseRequirements.Add(str);

                        cropInfo.seed = obj;
                        objects.Add(obj);
                    }
                }

                if (Directory.Exists(Path.Combine(dir, "FruitTrees")))
                {
                    foreach (var fruitTreeDir in Directory.EnumerateDirectories(Path.Combine(dir, "FruitTrees")))
                    {
                        if (!File.Exists(Path.Combine(fruitTreeDir, "tree.json")))
                            continue;
                        var fruitTreeInfo = Helper.ReadJsonFile<FruitTreeData>(Path.Combine(fruitTreeDir, "tree.json"));
                        fruitTreeInfo.directory = Path.Combine("ContentPacks", Path.GetFileName(dir), "FruitTrees", Path.GetFileName(fruitTreeDir));
                        fruitTrees.Add(fruitTreeInfo);

                        var obj = new ObjectData();
                        obj.directory = fruitTreeInfo.directory;
                        obj.imageName = "sapling.png";
                        obj.Name = fruitTreeInfo.SaplingName;
                        obj.Description = fruitTreeInfo.SaplingDescription;
                        obj.Category = ObjectData.Category_.Seeds;
                        obj.Price = fruitTreeInfo.SaplingPurchasePrice;

                        obj.CanPurchase = true;
                        obj.PurchaseFrom = fruitTreeInfo.SsaplingPurchaseFrom;
                        obj.PurchasePrice = fruitTreeInfo.SaplingPurchasePrice;

                        fruitTreeInfo.sapling = obj;
                        objects.Add(obj);
                    }
                }
            }

            objectIds = AssignIds("objects", StartingObjectId, objects.ToList<DataNeedsId>());
            cropIds = AssignIds("crops", StartingCropId, crops.ToList<DataNeedsId>());
            fruitTreeIds = AssignIds("fruittrees", StartingFruitTreeId, fruitTrees.ToList<DataNeedsId>());

            var editors = ((IList<IAssetEditor>)helper.Content.GetType().GetProperty("AssetEditors", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetValue(Helper.Content));
            editors.Add(new ContentInjector());
        }

        private void menuChanged(object sender, EventArgsClickableMenuChanged args)
        {
            var menu = args.NewMenu as ShopMenu;
            if (menu == null || menu.portraitPerson == null)
                return;

            //if (menu.portraitPerson.name == "Pierre")
            {
                Log.trace($"Adding objects to {menu.portraitPerson.name}'s shop");

                var forSale = Helper.Reflection.GetPrivateValue<List<Item>>(menu, "forSale");
                var itemPriceAndStock = Helper.Reflection.GetPrivateValue<Dictionary<Item, int[]>>(menu, "itemPriceAndStock");

                var precondMeth = Helper.Reflection.GetPrivateMethod(Game1.currentLocation, "checkEventPrecondition");
                foreach (var obj in objects)
                {
                    if ( obj.Recipe != null && obj.Recipe.CanPurchase )
                    {
                        if (obj.Recipe.PurchaseFrom != menu.portraitPerson.name)
                            continue;
                        if (Game1.player.craftingRecipes.ContainsKey(obj.Name) || Game1.player.cookingRecipes.ContainsKey(obj.Name))
                            continue;
                        if (obj.Recipe.PurchaseRequirements != null && obj.Recipe.PurchaseRequirements.Count > 0 &&
                            precondMeth.Invoke<int>(new object[] { obj.Recipe.GetPurchaseRequirementString() }) == -1)
                            continue;
                        var recipeObj = new StardewValley.Object(obj.id, 1, true, obj.Recipe.PurchasePrice, 0);
                        forSale.Add(recipeObj);
                        itemPriceAndStock.Add(recipeObj, new int[] { obj.Recipe.PurchasePrice, 1 });
                        Log.trace($"\tAdding recipe for {obj.Name}");
                    }
                    if (!obj.CanPurchase)
                        continue;
                    if (obj.PurchaseFrom != menu.portraitPerson.name)
                        continue;
                    if (obj.PurchaseRequirements != null && obj.PurchaseRequirements.Count > 0 &&
                        precondMeth.Invoke<int>(new object[] { obj.GetPurchaseRequirementString() }) == -1)
                        continue;
                    Item item = new StardewValley.Object(Vector2.Zero, obj.id, int.MaxValue);
                    forSale.Add(item);
                    itemPriceAndStock.Add(item, new int[] { obj.PurchasePrice, int.MaxValue });
                    Log.trace($"\tAdding {obj.Name}");
                }
            }
        }

        private void afterLoad( object sender, EventArgs args )
        {
            foreach ( var obj in objects )
            {
                if ( obj.Recipe != null && obj.Recipe.IsDefault && !Game1.player.knowsRecipe(obj.Name) )
                {
                    if ( obj.Category == ObjectData.Category_.Cooking )
                    {
                        Game1.player.cookingRecipes.Add(obj.Name, 0);
                    }
                    else
                    {
                        Game1.player.cookingRecipes.Add(obj.Name, 0);
                    }
                }
            }
        }

        private const int StartingObjectId = 2000;
        private const int StartingCropId = 100;
        private const int StartingFruitTreeId = 20;
        internal IList<ObjectData> objects = new List<ObjectData>();
        internal IList<CropData> crops = new List<CropData>();
        internal IList<FruitTreeData> fruitTrees = new List<FruitTreeData>();
        private IDictionary<string, int> objectIds;
        private IDictionary<string, int> cropIds;
        private IDictionary<string, int> fruitTreeIds;

        public int ResolveObjectId( object data )
        {
            if (data.GetType() == typeof(long))
                return (int)(long)data;
            else
                return objectIds[ (string) data ];
        }

        private Dictionary<string, int> AssignIds( string type, int starting, IList<DataNeedsId> data )
        {
            var saved = Helper.ReadJsonFile<Dictionary<string, int>>(Path.Combine(Helper.DirectoryPath,$"ids-{type}.json"));
            Dictionary<string, int> ids = new Dictionary<string, int>();

            int currId = starting;
            // First, populate saved IDs
            foreach ( var d in data )
            {
                if (saved != null && saved.ContainsKey(d.Name))
                {
                    ids.Add(d.Name, saved[d.Name]);
                    currId = Math.Max(currId, saved[d.Name] + 1);
                    d.id = ids[d.Name];
                }
            }
            // Next, add in new IDs
            foreach (var d in data)
            {
                if (d.id == -1)
                {
                    ids.Add(d.Name, currId++);
                    if (type == "objects" && ((ObjectData)d).IsColored)
                        ++currId;
                    d.id = ids[d.Name];
                }
            }

            Helper.WriteJsonFile(Path.Combine(Helper.DirectoryPath, $"ids-{type}.json"), ids);
            return ids;
        }
    }
}
