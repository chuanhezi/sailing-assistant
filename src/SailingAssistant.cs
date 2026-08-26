using System;
using System.Collections.Generic;
using PirateModAPI;
using UnityEngine;

namespace SailingAssistant
{
    public sealed class Extension : IPirateExtension, IRequiresPirateApi
    {
        private IPirateApi api;
        private AssistantOverlay overlay;

        public string Id { get { return "chuan.sailing-assistant"; } }
        public string Name { get { return "综合航海助手 / Sailing Assistant"; } }
        public Version Version { get { return new Version(1, 3, 0); } }
        public Version MinimumApiVersion { get { return new Version(1, 4, 0); } }

        public void Initialize(IPirateApi pirateApi)
        {
            api = pirateApi;
            overlay = new GameObject("SailingAssistantOverlay").AddComponent<AssistantOverlay>();
            UnityEngine.Object.DontDestroyOnLoad(overlay.gameObject);
            overlay.Initialize(api);
            api.SceneChanged += OnSceneChanged;
            api.Log.Info("Sailing Assistant initialized.");
        }

        public void Shutdown()
        {
            if (api != null)
                api.SceneChanged -= OnSceneChanged;
            if (overlay != null)
                UnityEngine.Object.Destroy(overlay.gameObject);
            if (api != null)
                api.Log.Info("Sailing Assistant shut down.");
        }

        private void OnSceneChanged(object sender, SceneChangedEventArgs args)
        {
            if (overlay == null)
                return;
            overlay.SetSceneActive(!string.Equals(args.CurrentScene, "mainmenu", StringComparison.Ordinal) &&
                                   !string.Equals(args.CurrentScene, "start", StringComparison.Ordinal));
        }

    }

    public sealed class AssistantOverlay : MonoBehaviour
    {
        private const int WindowId = 714203;
        private const float VisibleRefreshInterval = 2f;
        private const float HiddenRefreshInterval = 5f;
        private const float SlowRefreshInterval = 10f;
        private IPirateApi api;
        private Rect windowRect;
        private Vector2 scrollPosition;
        private float nextRefresh;
        private float nextSlowRefresh;
        private int selectedTab;
        private int language;
        private bool visible;
        private bool sceneActive;
        private string statusMessage = string.Empty;
        private Font nativeFont;
        private Texture2D panelTexture;
        private Texture2D buttonTexture;
        private Texture2D buttonHoverTexture;
        private GUIStyle windowStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle toolbarStyle;
        private bool stylesReady;
        private PlayerSnapshot player;
        private IReadOnlyList<ShipSnapshot> worldShips;
        private IReadOnlyList<ShipCargoSnapshot> shipCargo;
        private WarehouseSnapshot warehouse;
        private int shipFilter;
        private PortSnapshot currentPort;
        private IReadOnlyList<QuestSnapshot> quests;
        private SaveSnapshot save;

        public void Initialize(IPirateApi pirateApi)
        {
            api = pirateApi;
            int layoutVersion = api.Config.GetInt32("layoutVersion", 0);
            int x = layoutVersion < 2 ? 360 : api.Config.GetInt32("windowX", 360);
            int y = layoutVersion < 2 ? 140 : api.Config.GetInt32("windowY", 140);
            language = api.Config.GetInt32("language", 0) == 1 ? 1 : 0;
            visible = false;
            api.Config.SetInt32("layoutVersion", 3);
            api.Config.SetBoolean("visible", false);
            sceneActive = false;
            windowRect = new Rect(x, y, 440, 438);
            RefreshData();
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (api != null)
                api.Config.SetBoolean("visible", value);
            if (value)
            {
                nextRefresh = 0f;
                nextSlowRefresh = 0f;
                RefreshData();
            }
        }

        public void SetSceneActive(bool value)
        {
            sceneActive = value && player != null && player.IsAvailable;
        }

        private void CreateStyles()
        {
            if (stylesReady)
                return;
            panelTexture = MakeTexture(new Color(0.12f, 0.075f, 0.035f, 1f));
            buttonTexture = MakeTexture(new Color(0.40f, 0.24f, 0.11f, 0.98f));
            buttonHoverTexture = MakeTexture(new Color(0.58f, 0.37f, 0.16f, 0.98f));
            windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.padding = new RectOffset(12, 12, 28, 10);
            windowStyle.fontSize = 18;
            SetAllStates(windowStyle, panelTexture, new Color(1f, 0.84f, 0.60f, 1f));
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = new Color(1f, 0.90f, 0.72f, 1f);
            labelStyle.wordWrap = true;
            buttonStyle = new GUIStyle(GUI.skin.button);
            SetAllStates(buttonStyle, buttonTexture, new Color(1f, 0.90f, 0.72f, 1f));
            buttonStyle.hover.background = buttonHoverTexture;
            buttonStyle.active.background = buttonHoverTexture;
            buttonStyle.hover.textColor = Color.white;
            toolbarStyle = new GUIStyle(buttonStyle);
            toolbarStyle.fontSize = 14;
            toolbarStyle.onNormal.background = buttonHoverTexture;
            toolbarStyle.onNormal.textColor = Color.white;
            toolbarStyle.onHover.background = buttonHoverTexture;
            toolbarStyle.onActive.background = buttonHoverTexture;
            toolbarStyle.onFocused.background = buttonHoverTexture;
            stylesReady = true;
        }

        private static void SetAllStates(GUIStyle style, Texture2D background, Color textColor)
        {
            style.normal.background = background; style.normal.textColor = textColor;
            style.hover.background = background; style.hover.textColor = textColor;
            style.active.background = background; style.active.textColor = textColor;
            style.focused.background = background; style.focused.textColor = textColor;
            style.onNormal.background = background; style.onNormal.textColor = textColor;
            style.onHover.background = background; style.onHover.textColor = textColor;
            style.onActive.background = background; style.onActive.textColor = textColor;
            style.onFocused.background = background; style.onFocused.textColor = textColor;
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void FindNativeFont()
        {
            if (nativeFont != null)
                return;
            TextMesh startText = GameObject.Find("Start") == null ? null : GameObject.Find("Start").GetComponentInChildren<TextMesh>(true);
            if (startText != null && startText.font != null)
                nativeFont = startText.font;
            if (nativeFont == null)
            {
                TextMesh[] texts = Resources.FindObjectsOfTypeAll<TextMesh>();
                foreach (TextMesh text in texts)
                {
                    if (text != null && text.font != null)
                    {
                        nativeFont = text.font;
                        break;
                    }
                }
            }
            if (nativeFont != null)
            {
                windowStyle.font = nativeFont;
                labelStyle.font = nativeFont;
                buttonStyle.font = nativeFont;
                toolbarStyle.font = nativeFont;
            }
        }

        private void Update()
        {
            if (api == null)
                return;
            if (Time.unscaledTime < nextRefresh)
                return;
            nextRefresh = Time.unscaledTime + (visible ? VisibleRefreshInterval : HiddenRefreshInterval);
            RefreshData();
        }

        private void RefreshData()
        {
            try { player = api.Player.Snapshot(); } catch (Exception exception) { api.Log.Warning("读取玩家状态失败：" + exception.Message); }
            sceneActive = player != null && player.IsAvailable;
            if (!visible)
                return;

            if (selectedTab == 0 || selectedTab == 1)
                try { worldShips = api.Ships.GetWorldShips(); } catch (Exception exception) { api.Log.Warning("读取当前海域船只失败：" + exception.Message); }
            if (selectedTab == 0 || selectedTab == 2)
            {
                try { shipCargo = api.Cargo.GetPlayerShipCargo(); } catch (Exception exception) { api.Log.Warning("读取玩家船舱失败：" + exception.Message); }
                try { warehouse = api.Cargo.GetPortWarehouse(); } catch (Exception exception) { api.Log.Warning("读取港口仓库失败：" + exception.Message); }
            }

            bool refreshSlowData = Time.unscaledTime >= nextSlowRefresh;
            if (refreshSlowData)
            {
                nextSlowRefresh = Time.unscaledTime + SlowRefreshInterval;
                if (selectedTab == 0)
                {
                    try { currentPort = api.Ports.Current(); } catch (Exception exception) { api.Log.Warning("读取港口状态失败：" + exception.Message); }
                    try { save = api.Save.Status(); } catch (Exception exception) { api.Log.Warning("读取存档状态失败：" + exception.Message); }
                }
                if (selectedTab == 0 || selectedTab == 3)
                    try { quests = api.Quests.GetActiveQuests(); } catch (Exception exception) { api.Log.Warning("读取任务状态失败：" + exception.Message); }
            }
            statusMessage = T("更新于 ", "Updated ") + DateTime.Now.ToString("HH:mm:ss");
        }

        private void OnGUI()
        {
            if (api == null)
                return;
            CreateStyles();
            FindNativeFont();
            if (!sceneActive)
                return;
            GUI.depth = 50;
            Color oldColor = GUI.color;
            Color oldBackgroundColor = GUI.backgroundColor;
            Color oldContentColor = GUI.contentColor;
            GUI.color = Color.white;
            GUI.backgroundColor = Color.white;
            GUI.contentColor = Color.white;
            try
            {
                if (!visible)
                {
                    if (GUI.Button(new Rect(18, Mathf.Max(48, (Screen.height - 32) * 0.5f), 126, 32), T("航海助手", "Assistant"), buttonStyle))
                        SetVisible(true);
                    return;
                }

                windowRect.x = Mathf.Clamp(windowRect.x, 0, Mathf.Max(0, Screen.width - windowRect.width));
                windowRect.y = Mathf.Clamp(windowRect.y, 38, Mathf.Max(38, Screen.height - windowRect.height));
                windowRect = GUI.Window(WindowId, windowRect, DrawWindow, T("综合航海助手", "Sailing Assistant"), windowStyle);
            }
            finally
            {
                GUI.color = oldColor;
                GUI.backgroundColor = oldBackgroundColor;
                GUI.contentColor = oldContentColor;
            }
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            int nextTab = GUILayout.Toolbar(selectedTab, new[] { T("总览", "Overview"), T("舰队", "Ships"), T("货舱", "Cargo"), T("任务", "Quests") }, toolbarStyle);
            if (nextTab != selectedTab)
            {
                selectedTab = nextTab;
                scrollPosition = Vector2.zero;
                nextSlowRefresh = 0f;
                RefreshData();
            }
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(336));
            if (selectedTab == 0) DrawOverview();
            else if (selectedTab == 1) DrawFleet();
            else if (selectedTab == 2) DrawCargo();
            else DrawQuests();
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("刷新", "Refresh"), buttonStyle, GUILayout.Width(68))) { nextSlowRefresh = 0f; RefreshData(); }
            if (GUILayout.Button(T("保存", "Save"), buttonStyle, GUILayout.Width(68))) SaveGame();
            if (GUILayout.Button(language == 0 ? "中文" : "English", buttonStyle, GUILayout.Width(62))) ToggleLanguage();
            GUILayout.FlexibleSpace();
            GUILayout.Label(statusMessage, labelStyle);
            if (GUILayout.Button(T("收起", "Hide"), buttonStyle, GUILayout.Width(54))) SetVisible(false);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, windowRect.width, 22));
        }

        private void DrawOverview()
        {
            int playerShipCount = 0;
            int aiShipCount = 0;
            if (worldShips != null)
            {
                foreach (ShipSnapshot ship in worldShips)
                {
                    if (ship.IsPlayer) playerShipCount++;
                    else aiShipCount++;
                }
            }
            int cargoCapacity = 0;
            int cargoWeight = 0;
            if (shipCargo != null)
            {
                foreach (ShipCargoSnapshot ship in shipCargo)
                {
                    cargoCapacity += ship.Capacity;
                    cargoWeight += ship.TotalWeight;
                }
            }
            Label(T("玩家状态", "Player Status"));
            Label(T("名称：", "Name: ") + Safe(player == null ? string.Empty : player.Name));
            Label(T("金币：", "Gold: ") + (player == null ? 0 : player.Gold) + T("    等级：", "    Level: ") + (player == null ? 0 : player.Level) + T("    经验：", "    XP: ") + (player == null ? 0 : player.Experience));
            Label(T("第 ", "Day ") + (player == null ? 0 : player.Day) + T(" 天 ", "  ") + (player == null ? 0 : player.Hour) + T(" 时    区域：", ":00    Area: ") + (player == null ? -1 : player.Area));
            GUILayout.Space(8);
            Label(T("当前海域：玩家舰船 ", "Current area: Player ships ") + playerShipCount + T(" 艘    AI 舰船 ", "    AI ships ") + aiShipCount + T(" 艘", string.Empty));
            Label(T("玩家船舱：", "Player cargo: ") + cargoWeight + " / " + cargoCapacity);
            Label(player != null && player.IsSailing
                ? T("当前状态：公海航行", "Status: At sea")
                : T("当前港口：", "Current port: ") + Safe(currentPort != null && currentPort.IsAvailable ? currentPort.Name : string.Empty));
            Label(T("进行中的任务：", "Active quests: ") + (quests == null ? 0 : quests.Count));
            GUILayout.Space(8);
            Label(T("存档：", "Save: ") + (save != null && save.HasSave ? Safe(save.SaveName) : T("未检测到存档", "No save detected")) + (save != null && save.CanSave ? T("（可保存）", " (available)") : T("（当前不可保存）", " (unavailable)")));
        }

        private void DrawFleet()
        {
            shipFilter = GUILayout.Toolbar(shipFilter, new[] { T("全部", "All"), T("玩家", "Player"), "AI" }, toolbarStyle);
            GUILayout.Space(6);
            if (worldShips == null || worldShips.Count == 0)
            {
                Label(T("当前海域没有已生成的舰船。", "No spawned ships in the current area."));
                return;
            }
            int visibleCount = 0;
            foreach (ShipSnapshot ship in worldShips)
            {
                if ((shipFilter == 1 && !ship.IsPlayer) || (shipFilter == 2 && !ship.IsAi))
                    continue;
                visibleCount++;
                Label(LocalizedName(ship.Name, ship.EnglishName) + "    [" + (ship.IsPlayer ? T("玩家", "Player") : "AI") + "]" + (ship.IsActive ? T("  当前舰船", "  Active ship") : string.Empty));
                Label(T("船体：", "Hull: ") + ship.Hull + " / " + ship.MaxHull + T("    船员：", "    Crew: ") + ship.Crew + " / " + ship.MaxCrew);
                Label(T("船帆：", "Sails: ") + ship.Sail + " / " + ship.MaxSail + T("    火炮：", "    Cannons: ") + ship.Cannons + T(" 门", string.Empty));
                Label(T("航速：", "Speed: ") + ship.SpeedKnots.ToString("0.0") + T(" 节    状态：", " kn    Status: ") + (ship.IsAnchored ? T("已抛锚", "Anchored") : T("航行中", "Underway")));
                GUILayout.Space(7);
            }
            if (visibleCount == 0)
                Label(T("当前筛选条件下没有舰船。", "No ships match the current filter."));
        }

        private void DrawCargo()
        {
            bool isSailing = player != null && player.IsSailing;
            Label(isSailing ? T("公海航行", "At Sea") : T("港口停泊", "Docked in Port"));
            GUILayout.Space(5);
            if (shipCargo == null || shipCargo.Count == 0)
            {
                Label(T("当前场景没有可用的玩家舰船货舱数据。", "No player ship cargo data is available."));
            }
            else
            {
                foreach (ShipCargoSnapshot ship in shipCargo)
                {
                    Label("[" + LocalizedName(ship.ShipName, ship.EnglishShipName) + "]");
                    Label(T("容量：", "Capacity: ") + ship.Capacity + T("    总重：", "    Total weight: ") + ship.TotalWeight + T("    剩余：", "    Free: ") + ship.FreeSpace);
                    if (ship.Items.Count == 0)
                        Label(T("货舱为空。", "Cargo hold is empty."));
                    else
                    {
                        foreach (CargoItemSnapshot item in ship.Items)
                            Label(LocalizedName(item.Name, item.EnglishName) + T("：", ": ") + item.Quantity + T(" 件    单重 ", "    Unit weight ") + item.UnitWeight + T("    合计 ", "    Total ") + item.TotalWeight);
                    }
                    GUILayout.Space(8);
                }
            }

            if (isSailing)
                return;
            Label(T("港口仓库", "Port Warehouse") + (warehouse != null && !string.IsNullOrEmpty(warehouse.PortName) ? " - " + warehouse.PortName : string.Empty));
            if (warehouse == null || !warehouse.IsAvailable)
            {
                Label(T("当前港口没有可用的仓库数据。", "No warehouse data is available at this port."));
                return;
            }
            Label(T("容量：", "Capacity: ") + warehouse.Capacity + T("    总重：", "    Total weight: ") + warehouse.TotalWeight + T("    剩余：", "    Free: ") + warehouse.FreeSpace);
            GUILayout.Space(5);
            foreach (CargoItemSnapshot item in warehouse.Items)
                Label(LocalizedName(item.Name, item.EnglishName) + T("：", ": ") + item.Quantity + T(" 件    重量 ", "    Weight ") + item.TotalWeight + T("    买入 ", "    Buy ") + item.BuyPrice + T(" / 卖出 ", " / Sell ") + item.SellPrice);
        }

        private void DrawQuests()
        {
            if (quests == null || quests.Count == 0)
            {
                Label(T("当前没有进行中的支线任务。", "There are no active side quests."));
                return;
            }
            foreach (QuestSnapshot quest in quests)
            {
                Label("#" + quest.Id + "  " + Safe(quest.Name) + "  " + Safe(quest.Type));
                if (!string.IsNullOrEmpty(quest.Description)) Label("  " + Safe(quest.Description));
                Label(T("  起点：", "  Origin: ") + Safe(quest.GiverTown) + T("    目标：", "    Destination: ") + Safe(quest.TargetTown));
                GUILayout.Space(4);
            }
        }

        private void SaveGame()
        {
            if (api.Save.Save())
                statusMessage = T("游戏已保存", "Game saved");
            else
                statusMessage = T("当前无法保存", "Cannot save now");
        }

        private void ToggleLanguage()
        {
            language = language == 0 ? 1 : 0;
            api.Config.SetInt32("language", language);
            api.Config.Save();
            statusMessage = language == 0 ? "已切换为中文" : "Switched to English";
        }

        private void Label(string text)
        {
            GUILayout.Label(text, labelStyle);
        }

        private string T(string chinese, string english)
        {
            return language == 1 ? english : chinese;
        }

        private string LocalizedName(string gameName, string englishName)
        {
            return Safe(language == 1 && !string.IsNullOrEmpty(englishName) ? englishName : gameName);
        }

        private string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? T("无", "None") : value;
        }

        private void OnDestroy()
        {
            if (api == null)
                return;
            api.Config.SetInt32("windowX", (int)windowRect.x);
            api.Config.SetInt32("windowY", (int)windowRect.y);
            api.Config.SetInt32("language", language);
            api.Config.SetBoolean("visible", visible);
            api.Config.Save();
            if (panelTexture != null) UnityEngine.Object.Destroy(panelTexture);
            if (buttonTexture != null) UnityEngine.Object.Destroy(buttonTexture);
            if (buttonHoverTexture != null) UnityEngine.Object.Destroy(buttonHoverTexture);
        }
    }
}
