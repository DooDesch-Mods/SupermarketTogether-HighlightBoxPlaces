using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HighlightPlus;
using Mirror;
using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HighlightBoxPlaces;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, "HighlightBoxPlaces", MyPluginInfo.PLUGIN_VERSION)]
public sealed class Plugin : BaseUnityPlugin
{
    private sealed class OwnedHighlight
    {
        internal Transform Target;
        internal HighlightEffect Effect;
        internal UnityEngine.UI.Outline UiEffect;
        internal int RendererHash;
        internal Color Color;
    }

    private readonly Dictionary<int, OwnedHighlight> owned = new Dictionary<int, OwnedHighlight>();
    private readonly Dictionary<int, Color> wanted = new Dictionary<int, Color>();
    private readonly Dictionary<int, Transform> targets = new Dictionary<int, Transform>();
    private readonly List<Data_Container> shelves = new List<Data_Container>();
    private readonly List<Data_Container> storage = new List<Data_Container>();
    private readonly List<int> obsolete = new List<int>();
    private ConfigEntry<bool> featureEnabled;
    private ConfigEntry<bool> showStorage;
    private ConfigEntry<bool> throughWalls;
    private ConfigEntry<float> refreshInterval;
    private ConfigEntry<Color> emptyColor;
    private ConfigEntry<Color> partialColor;
    private ConfigEntry<Color> fullColor;
    private ConfigEntry<Color> storageColor;
    private int product = -1;
    private float nextRefresh;
    private float nextIndex;
    internal int OwnedSignCount
    {
        get
        {
            int count = 0;
            foreach (OwnedHighlight entry in owned.Values) if (entry.UiEffect) count++;
            return count;
        }
    }

    private void Awake()
    {
        featureEnabled = Config.Bind("General", "Enabled", true, "Show matching locations for the box in your hands.");
        showStorage = Config.Bind("Display", "Storage", true, "Show matching storage boxes and reserved storage slots.");
        throughWalls = Config.Bind("Display", "Through Walls", true, "Show outlines through walls and other shelves.");
        refreshInterval = Config.Bind("Display", "Refresh Interval", 0.25f,
            new ConfigDescription("Seconds between stock and outline updates.", new AcceptableValueRange<float>(0.1f, 2f)));
        emptyColor = Config.Bind("Colors", "Empty", new Color(1f, 0.25f, 0.2f), "An assigned shelf row has no stock.");
        partialColor = Config.Bind("Colors", "Needs Stock", new Color(1f, 0.75f, 0.15f), "A matching shelf row has room for more stock.");
        fullColor = Config.Bind("Colors", "Full", new Color(0.2f, 0.9f, 0.4f), "Matching shelf rows are full.");
        storageColor = Config.Bind("Colors", "Storage", new Color(0.15f, 0.65f, 1f), "A storage box or reserved storage slot matches.");
        SceneManager.sceneLoaded += SceneLoaded;
        Logger.LogInfo("HighlightBoxPlaces loaded. Outlines use this player's held product and local stock data.");
    }

    private void Update()
    {
        PlayerNetwork player = NetworkClient.localPlayer ? NetworkClient.localPlayer.GetComponent<PlayerNetwork>() : null;
        if (!player && FirstPersonController.Instance)
            player = FirstPersonController.Instance.GetComponent<PlayerNetwork>();
        bool inputAllowed = FirstPersonController.Instance && FirstPersonController.Instance.allowPlayerInput;
        int current = featureEnabled.Value && inputAllowed && player && player.isLocalPlayer && player.equippedItem == 1
            ? player.extraParameter1 : -1;
        if (!ProductListing.Instance || current < 0 || current >= ProductListing.Instance.productsData.Length)
            current = -1;
        if (current != product)
        {
            Clear();
            product = current;
            nextRefresh = 0f;
        }
        if (product < 0 || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + refreshInterval.Value;
        RefreshForProduct(product);
    }

    internal void RefreshForProduct(int productId)
    {
        if (!ProductListing.Instance || productId < 0 || productId >= ProductListing.Instance.productsData.Length)
        {
            Clear();
            return;
        }
        if (Time.unscaledTime >= nextIndex)
        {
            nextIndex = Time.unscaledTime + 1f;
            Reindex(shelves, "Level_SupermarketProps/Shelves");
            Reindex(storage, "Level_SupermarketProps/StorageShelves");
        }
        wanted.Clear();
        targets.Clear();
        ProductListing.ProductData data = ProductListing.Instance.productsData[productId];
        foreach (Data_Container shelf in shelves)
        {
            if (!shelf || !shelf.gameObject.activeInHierarchy || shelf.productInfoArray == null) continue;
            int capacity = StockRules.Capacity(shelf.shelfLength, shelf.shelfWidth, shelf.shelfHeight,
                data.colliderSize.x, data.colliderSize.y, data.colliderSize.z, data.isStackable);
            if (capacity <= 0 || shelf.containerClass != data.productContainerClass ||
                (shelf.isVolumeRestricted && data.colliderSize.x * data.colliderSize.y * data.colliderSize.z < shelf.productVolumeLimit)) continue;
            Transform labels = shelf.transform.Find("Labels");
            int matching = 0;
            int count = 0;
            bool room = false;
            for (int row = 0; row + 1 < shelf.productInfoArray.Length; row += 2)
            {
                if (shelf.productInfoArray[row] != productId) continue;
                int quantity = Mathf.Max(0, shelf.productInfoArray[row + 1]);
                matching++;
                count += quantity;
                room |= quantity < capacity;
                if (labels && row / 2 < labels.childCount)
                    Want(labels.GetChild(row / 2), ColorFor(StockRules.State(quantity, capacity)));
            }
            if (matching > 0)
                Want(shelf.transform, ColorFor(count == 0 ? FillState.Empty : room ? FillState.Partial : FillState.Full));
        }
        if (showStorage.Value)
        {
            foreach (Data_Container shelf in storage)
            {
                if (!shelf || !shelf.gameObject.activeInHierarchy || shelf.productInfoArray == null) continue;
                Transform boxes = shelf.transform.Find("BoxContainer");
                Transform signs = shelf.transform.Find("CanvasSigns");
                bool matched = false;
                for (int row = 0; row + 1 < shelf.productInfoArray.Length; row += 2)
                {
                    if (shelf.productInfoArray[row] != productId) continue;
                    matched = true;
                    int slot = row / 2;
                    if (boxes && slot < boxes.childCount) Want(boxes.GetChild(slot), storageColor.Value);
                    if (signs && slot < signs.childCount && signs.GetChild(slot).gameObject.activeInHierarchy)
                        Want(signs.GetChild(slot), storageColor.Value);
                }
                if (matched) Want(shelf.transform, storageColor.Value);
            }
        }
        Apply();
    }

    private static void Reindex(List<Data_Container> destination, string path)
    {
        destination.Clear();
        GameObject parent = GameObject.Find(path);
        if (!parent) return;
        for (int i = 0; i < parent.transform.childCount; i++)
        {
            Data_Container container = parent.transform.GetChild(i).GetComponent<Data_Container>();
            if (container) destination.Add(container);
        }
    }

    private Color ColorFor(FillState state)
    {
        return state == FillState.Empty ? emptyColor.Value : state == FillState.Full ? fullColor.Value : partialColor.Value;
    }

    private void Want(Transform target, Color color)
    {
        if (!target) return;
        int id = target.GetInstanceID();
        targets[id] = target;
        wanted[id] = color;
    }

    private void Apply()
    {
        obsolete.Clear();
        foreach (KeyValuePair<int, OwnedHighlight> pair in owned)
            if (!wanted.ContainsKey(pair.Key) || !pair.Value.Target || (!pair.Value.Effect && !pair.Value.UiEffect)) obsolete.Add(pair.Key);
        foreach (int id in obsolete) Remove(id);
        foreach (KeyValuePair<int, Color> pair in wanted)
        {
            Transform target = targets[pair.Key];
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                // Reserved storage signs use uGUI geometry, which HighlightEffect cannot render.
                if (target.GetComponent<UnityEngine.UI.Graphic>())
                {
                    if (!owned.TryGetValue(pair.Key, out OwnedHighlight sign))
                    {
                        sign = new OwnedHighlight { Target = target, UiEffect = target.gameObject.AddComponent<UnityEngine.UI.Outline>() };
                        sign.UiEffect.effectDistance = new Vector2(2f, -2f);
                        owned.Add(pair.Key, sign);
                    }
                    sign.UiEffect.effectColor = pair.Value;
                }
                else if (owned.ContainsKey(pair.Key)) Remove(pair.Key);
                continue;
            }
            int hash = 17;
            foreach (Renderer renderer in renderers) hash = unchecked(hash * 31 + renderer.GetInstanceID());
            bool added = false;
            if (!owned.TryGetValue(pair.Key, out OwnedHighlight entry))
            {
                // A separate owner leaves the game's hover effects and other mods' profiles unchanged.
                GameObject owner = new GameObject("HighlightBoxPlaces Outline");
                owner.transform.SetParent(transform, false);
                HighlightEffect effect = owner.AddComponent<HighlightEffect>();
                effect.fadeInDuration = 0f;
                effect.fadeOutDuration = 0f;
                effect.outline = 1f;
                effect.glow = 0f;
                effect.outlineIndependent = true;
                effect.outlineQuality = HighlightPlus.QualityLevel.Medium;
                entry = new OwnedHighlight { Target = target, Effect = effect };
                owned.Add(pair.Key, entry);
                added = true;
            }
            HighlightPlus.Visibility visibility = throughWalls.Value ? HighlightPlus.Visibility.AlwaysOnTop : HighlightPlus.Visibility.Normal;
            bool changed = added || entry.Color != pair.Value || entry.Effect.outlineVisibility != visibility;
            if (added || entry.RendererHash != hash)
            {
                entry.Effect.SetTargets(target, renderers);
                entry.RendererHash = hash;
                changed = true;
            }
            if (changed)
            {
                entry.Color = pair.Value;
                entry.Effect.outlineColor = pair.Value;
                entry.Effect.outlineVisibility = visibility;
                entry.Effect.UpdateMaterialProperties();
            }
            entry.Effect.highlighted = true;
        }
    }

    private void Remove(int id)
    {
        OwnedHighlight entry = owned[id];
        if (entry.Effect)
        {
            entry.Effect.highlighted = false;
            entry.Effect.enabled = false;
            Destroy(entry.Effect.gameObject);
        }
        if (entry.UiEffect)
        {
            entry.UiEffect.enabled = false;
            Destroy(entry.UiEffect);
        }
        owned.Remove(id);
    }

    internal void Clear()
    {
        obsolete.Clear();
        obsolete.AddRange(owned.Keys);
        foreach (int id in obsolete) Remove(id);
    }

    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Clear();
        shelves.Clear();
        storage.Clear();
        nextIndex = nextRefresh = 0f;
        product = -1;
    }

    private void OnDisable() { Clear(); }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        Clear();
    }
}
