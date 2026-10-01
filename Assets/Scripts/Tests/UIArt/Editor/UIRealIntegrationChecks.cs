// 职责：显式隔离Play检查；Unity虚拟输入走真实业务，不控制桌面。远端箱/敌人使用位置夹具并单独记录。
// 维护Dada；交接docs/handoffs/Dada.handoff；规范AGENTS.md。不是人工通关或构建验收。
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Regrowth.Core;
using Regrowth.Runtime;
using Regrowth.Gameplay;
using Regrowth.UI;
using Regrowth.UI.Art;
using Regrowth.UI.Art.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Regrowth.Tests.UIArt.Editor
{
    [InitializeOnLoad]
    public static class UIRealIntegrationChecks
    {
        private const string Pending="UIRealIntegrationChecks.Pending";
        private const string Output="Logs/UIRealIntegration/";
        private static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();
        private static readonly StringBuilder report=new StringBuilder();
        private static readonly List<string> errors=new List<string>();
        private static Keyboard keyboard;
        private static Gamepad gamepad;
        private static int assertions;
        private static double deadline;
        static UIRealIntegrationChecks()
        {
            EditorApplication.playModeStateChanged+=s=>
            {
                if(s!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending,false)) { return; }
                SessionState.SetBool(Pending,false); Application.logMessageReceived+=Log;
                deadline=EditorApplication.timeSinceStartup+240;
                stack.Push(SessionState.GetBool("UIRealIntegrationChecks.EntryHead",false) ? CheckEntryHead() :
                    SessionState.GetBool("UIRealIntegrationChecks.AlignmentPrompt",false) ? CheckAlignmentPrompt() :
                    SessionState.GetBool("UIRealIntegrationChecks.Remaining",false) ? CheckRemaining() : Check()); EditorApplication.update+=Step;
            };
        }
        public static void BuildAndRun() { UIRealIntegrationRevision.Apply(); Run(); }
        public static void Run()
        {
            GameplayEntry.SetDirectSceneCheck(true);
            SessionState.SetBool("UIRealIntegrationChecks.EntryHead",false);
            SessionState.SetBool("UIRealIntegrationChecks.AlignmentPrompt",false);
            SessionState.SetBool("UIRealIntegrationChecks.Remaining",false);
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene(UIRealIntegrationRevision.MainMenu);
            SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
        }
        public static void RunRemaining()
        {
            GameplayEntry.SetDirectSceneCheck(true);
            SessionState.SetBool("UIRealIntegrationChecks.EntryHead",false);
            SessionState.SetBool("UIRealIntegrationChecks.AlignmentPrompt",false);
            Directory.CreateDirectory(Output);
            SessionState.SetBool("UIRealIntegrationChecks.Remaining",true);
            EditorSceneManager.OpenScene(UIFinalRevision.Level);
            SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
        }
        public static void RunAlignmentPrompt()
        {
            GameplayEntry.SetDirectSceneCheck(true);
            SessionState.SetBool("UIRealIntegrationChecks.EntryHead",false);
            UIAlignmentPromptRevision.Apply();
            Directory.CreateDirectory(Output);
            SessionState.SetBool("UIRealIntegrationChecks.AlignmentPrompt",true);
            EditorSceneManager.OpenScene(UIFinalRevision.Level);
            SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
        }
        private static IEnumerator CheckAlignmentPrompt()
        {
            InputSystem.settings=Object.Instantiate(InputSystem.settings);
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard=InputSystem.AddDevice<Keyboard>();
            yield return Wait(.3f);
            yield return WaitForOpeningIntro();
            var interactor=Object.FindFirstObjectByType<PlayerInteractor>();
            var hint=Object.FindFirstObjectByType<InteractionHintView>();
            var panel=Object.FindFirstObjectByType<ChoicePanel>();
            var root=Ref<GameObject>(hint,"hintRoot"); var text=Ref<TMP_Text>(hint,"label");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A));
            float until=Time.unscaledTime+3;
            while(!interactor.HasTarget && Time.unscaledTime<until) { yield return null; }
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return Wait(.1f);
            Assert(interactor.HasTarget && root.activeInHierarchy && text.text.StartsWith("[E]") && text.text.IndexOf("[E]",3)<0,"Real nearby target shows independent E prompt");
            report.AppendLine("Actual target="+interactor.InteractionId+"; visible prompt="+text.text);
            Capture("InteractionHint_NearChest",true);
            yield return KeyPress(Key.E);
            Assert(panel.IsOpen && !root.activeSelf,"E opens actual Choice and hides interaction hint");
            foreach(var card in panel.GetComponentsInChildren<CardVisual>())
            {
                Assert(card.DescriptionText.alignment==TextAlignmentOptions.Top && card.Title.alignment==TextAlignmentOptions.Center,"Both card text blocks horizontally centered");
                Assert(card.DescriptionText.margin==Vector4.zero && card.Title.margin==Vector4.zero,"No asymmetric text margins");
            }
            Capture("Cards_CenteredText",true);
            yield return KeyPress(Key.Escape);
            Assert(!panel.IsOpen && root.activeSelf,"Cancel restores current-target hint");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D)); until=Time.unscaledTime+3;
            while(interactor.HasTarget && Time.unscaledTime<until) { yield return null; }
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return Wait(.1f);
            Assert(!interactor.HasTarget && !root.activeSelf,"Leaving actual interaction range hides hint");
            report.AppendLine("PASS targeted text alignment / approach E / choosing hidden / cancel restored / leave hidden. No reward/combat rerun.");
        }

        public static void RunEntryHead()
        {
            GameplayEntry.RegisterBuildEntry();
            const string path="Assets/Prefabs/Hud/PlayerHud.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var body=root.GetComponent<BodyHudArt>();
                Ref<GameObject>(body,"torso").SetActive(false);
                Ref<GameObject>(body,"armsBase").SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            GameplayEntry.SetDirectSceneCheck(false);
            EditorSceneManager.OpenScene(UIFinalRevision.ArtScene); GameplayEntry.Refresh();
            Assert(!EditorSceneManager.playModeStartScene,"ArtTest keeps direct play");
            EditorSceneManager.OpenScene(UIFinalRevision.Level); GameplayEntry.Refresh();
            Assert(AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)==GameplayEntry.MainMenu,"Play from Level starts MainMenu");
            var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).ToArray();
            Assert(scenes[0].path==GameplayEntry.MainMenu && scenes[1].path==GameplayEntry.Level,"Build entry MainMenu then gameplay registered");
            Directory.CreateDirectory(Output);
            SessionState.SetBool("UIRealIntegrationChecks.EntryHead",true);
            SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
        }
        private static IEnumerator CheckEntryHead()
        {
            InputSystem.settings=Object.Instantiate(InputSystem.settings);
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard=InputSystem.AddDevice<Keyboard>(); yield return Wait(.4f);
            var menu=Object.FindFirstObjectByType<MainMenuController>();
            Assert(menu && SceneManager.GetActiveScene().path==GameplayEntry.MainMenu,"Actual Level Play enters MainMenu first");
            Click(Ref<Button>(menu,"startButton").gameObject);
            yield return Wait(1.2f);
            yield return WaitForOpeningIntro();
            Assert(SceneManager.GetActiveScene().path==GameplayEntry.Level,"Start loads registered gameplay scene");
            var state=Object.FindFirstObjectByType<PlayerState>();
            var hud=Object.FindFirstObjectByType<PlayerHud>(); var body=hud.GetComponent<BodyHudArt>();
            var torso=Ref<GameObject>(body,"torso"); var baseArms=Ref<GameObject>(body,"armsBase");
            Assert(((ILoadoutState)state).Items.Count==0 && !torso.activeSelf && !baseArms.activeSelf,"Empty real loadout hides torso and arm silhouette");
            Assert(hud.GetComponentsInChildren<Transform>().Any(x=>x.name=="Head"),"Head remains visible");
            Assert(Ref<GameObject>(body,"hpGroup").activeSelf && state.CurrentHealth>0,"HP and real body state remain intact");
            Capture("Gameplay_InitialHeadOnly",true);
            var interactor=Object.FindFirstObjectByType<PlayerInteractor>(); var panel=Object.FindFirstObjectByType<ChoicePanel>();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A)); float until=Time.unscaledTime+3;
            while(!interactor.HasTarget && Time.unscaledTime<until) { yield return null; }
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return Wait(.1f);
            Assert(interactor.InteractionId=="whitebox-chest-03","Walk to Chest03");
            string chosen=null;
            var chests=Object.FindObjectsByType<Chest>(FindObjectsSortMode.None).OrderBy(c=>c.name=="Chest_03" ? 0 : 1).ThenBy(c=>c.name).ToArray();
            foreach(var chest in chests)
            {
                if(chest.name!="Chest_03") { MoveFixture(state,(Vector2)chest.transform.position+new Vector2(0,-.6f)); yield return Wait(.1f); }
                yield return KeyPress(Key.E); Assert(panel.IsOpen,"Actual chest request");
                var card=panel.GetComponentsInChildren<CardVisual>().FirstOrDefault(c=>new[]{"legs","arms","tail","flame-tail"}.Contains(RuntimeId(c)));
                if(!card) { yield return KeyPress(Key.Escape); continue; }
                chosen=RuntimeId(card); Click(card.gameObject); yield return Wait(.2f); break;
            }
            Assert(chosen!=null && ((ILoadoutState)state).Items.Count>0 && torso.activeSelf,"First real body part reveals torso");
            Assert(baseArms.activeSelf==((ILoadoutState)state).Contains(LoadoutItemId.Arms),"Arm base shown only when arms actually owned");
            Capture("Gameplay_AfterFirstBodyPart",true);
            report.AppendLine("PASS MainMenu-first editor/build entry; initial head-only HUD with real HP preserved; real reward="+chosen+" reveals body. ArtTest direct play preserved.");
        }

        private static void Step()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline) { throw new Exception("Test timeout"); }
                if(stack.Count==0) { Finish(0); return; }
                var current=stack.Peek();
                if(!current.MoveNext()) { stack.Pop(); }
                else if(current.Current is IEnumerator child) { stack.Push(child); }
            }
            catch(Exception e) { report.AppendLine("FAIL: "+e); Finish(1); }
        }
        private static void Finish(int code)
        {
            EditorApplication.update-=Step; Application.logMessageReceived-=Log;
            if(errors.Count>0) { code=1; report.AppendLine(string.Join("\n",errors)); }
            report.AppendLine("Assertions="+assertions+"; runtime errors="+errors.Count+"; exit="+code);
            File.WriteAllText(Output+"checks.txt",report.ToString()); Debug.Log(report.ToString());
            if(keyboard!=null) { InputSystem.RemoveDevice(keyboard); }
            if(gamepad!=null) { InputSystem.RemoveDevice(gamepad); }
            EditorApplication.Exit(code);
        }
        private static void Log(string m,string s,LogType t)
        { if(t==LogType.Error || t==LogType.Exception || t==LogType.Assert) { errors.Add(m+"\n"+s); } }
        private static void Assert(bool ok,string message)
        { if(!ok) { throw new Exception(message); } assertions++; }
        private static T Ref<T>(Object owner,string name) where T:Object => UIFinalRevision.Ref<T>(owner,name);
        private static IEnumerator WaitForOpeningIntro()
        {
            var intro=Object.FindFirstObjectByType<Regrowth.UI.Intro.OpeningStoryIntro>();
            if(!intro) { yield break; }
            var root=Ref<GameObject>(intro,"introRoot");
            float until=Time.unscaledTime+35;
            while(root.activeSelf && Time.unscaledTime<until) { yield return null; }
            Assert(!root.activeSelf,"Opening intro completes before gameplay UI checks");
        }
        private static IEnumerator Wait(float seconds)
        { float end=Time.unscaledTime+seconds; while(Time.unscaledTime<end) { yield return null; } }
        private static IEnumerator KeyPress(Key key)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key)); yield return Wait(.15f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return Wait(.15f);
        }
        private static void Click(GameObject go)
        { ExecuteEvents.Execute(go,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler); }
        private static void MoveFixture(PlayerState state,Vector2 position)
        { var rb=state.GetComponent<Rigidbody2D>(); rb.position=position; rb.linearVelocity=Vector2.zero; Physics2D.SyncTransforms(); }
        private static string Id(CardVisual card) => new SerializedObject(card.GetComponent<ChoiceCardView>()).FindProperty("optionId")?.stringValue;
        private static string RuntimeId(CardVisual card)
        { return (string)typeof(ChoiceCardView).GetField("optionId",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(card.GetComponent<ChoiceCardView>()); }

        private static IEnumerator CheckRemaining()
        {
            InputSystem.settings=Object.Instantiate(InputSystem.settings);
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard=InputSystem.AddDevice<Keyboard>();
            yield return Wait(.3f);
            yield return WaitForOpeningIntro();
            yield return WaitForOpeningIntro();
            var state=Object.FindFirstObjectByType<PlayerState>();
            var panel=Object.FindFirstObjectByType<ChoicePanel>(); var hud=Object.FindFirstObjectByType<PlayerHud>();
            var interactor=Object.FindFirstObjectByType<PlayerInteractor>();
            var chests=Object.FindObjectsByType<Chest>(FindObjectsSortMode.None).OrderBy(c=>c.name).ToArray();
            bool found=false;
            foreach(var chest in chests)
            {
                MoveFixture(state,(Vector2)chest.transform.position+new Vector2(0,-.6f)); yield return Wait(.1f);
                interactor.RefreshTarget(); yield return KeyPress(Key.E);
                Assert(panel.IsOpen,"Real chest E (position fixture) "+chest.name);
                var card=panel.GetComponentsInChildren<CardVisual>().FirstOrDefault(c=>RuntimeId(c)=="flame-tail");
                if(!card) { panel.CancelCurrent(); yield return Wait(.1f); continue; }
                CheckCard(card); Click(card.gameObject); yield return Wait(.2f);
                Assert(chest.IsClaimed && ((ILoadoutState)state).Contains(LoadoutItemId.FlameTail),"Real flame-tail reward committed");
                Assert(Ref<GameObject>(hud.GetComponent<BodyHudArt>(),"flame").activeSelf,"Real receipt refreshes flame HUD");
                found=true; report.AppendLine("FlameTail acquired via E / actual card click from "+chest.name); break;
            }
            Assert(found,"Actual random chest pool offers FlameTail");
            int fire=0; state.GetComponent<PlayerFireAttack>().AttackStarted+=()=>fire++;
            yield return Wait(.6f); yield return KeyPress(Key.Q);
            Assert(fire==1 && state.GetComponent<PlayerFireAttack>().IsFiring,"Q starts actual fire action after action lock clears");
            Capture("Actual_Fire",true);
            report.AppendLine("Actual Fire AttackStarted=1; IsFiring=true; no state-write fixture used.");
            var target=Object.FindObjectsByType<EnemyBasic>(FindObjectsSortMode.None).Single(e=>e.name=="Enemy_06");
            int hp=state.CurrentHealth,events=0; ((IHealth)state).HealthChanged+=()=>events++;
            float fill=Ref<Image>(hud,"healthFill").fillAmount;
            MoveFixture(state,target.transform.position); float until=Time.unscaledTime+3;
            while(Time.unscaledTime<until && state.CurrentHealth==hp) { yield return null; }
            Assert(state.CurrentHealth<hp && events>0,"Enemy contact -> PlayerState -> HealthChanged");
            Assert(Ref<TMP_Text>(hud,"healthText").text==state.CurrentHealth.ToString() && Ref<Image>(hud,"healthFill").fillAmount<fill,"HP number/fill reflect same real damage");
            report.AppendLine("Enemy_06 contact position fixture at "+target.transform.position+"; HP "+hp+" -> "+state.CurrentHealth+"; HealthChanged="+events);
            Capture("Actual_Enemy_Damage",true);
            EditorSceneManager.LoadSceneInPlayMode(UIFinalRevision.ArtScene,new LoadSceneParameters(LoadSceneMode.Single)); yield return Wait(.3f);
            var preview=Object.FindFirstObjectByType<UIArtPreview>(); preview.CardsPage(); Ref<GameObject>(preview,"debugPanel").SetActive(false);
            Capture("ArtTest_Cards",false);
            yield return PixelOverlap(AssetDatabase.LoadAssetAtPath<CardPresentationCatalog>(UICardIconsRevision.CatalogPath));
            report.AppendLine("PASS remaining targeted checks. MainMenu/E/navigation/receipt coverage retained from the earlier run; not repeated.");
        }

        private static IEnumerator Check()
        {
            InputSystem.settings=Object.Instantiate(InputSystem.settings);
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard=InputSystem.AddDevice<Keyboard>(); gamepad=InputSystem.AddDevice<Gamepad>();
            yield return Wait(.4f);
            var menu=Object.FindFirstObjectByType<MainMenuController>(); Assert(menu,"Saved MainMenu loads");
            var walk=Object.FindFirstObjectByType<UIWalkPlayer>(); var seen=new HashSet<int>();
            float until=Time.unscaledTime+1;
            while(Time.unscaledTime<until) { seen.Add(walk.FrameIndex); yield return null; }
            Assert(seen.Count==3,"MainMenu real walk animation");
            Capture("MainMenu",false);
            Click(Ref<Button>(menu,"startButton").gameObject);
            yield return Wait(2);
            yield return WaitForOpeningIntro();
            Assert(SceneManager.GetActiveScene().path==UIFinalRevision.Level,"Actual Start Button loads Level_Whitebox");
            var state=Object.FindFirstObjectByType<PlayerState>();
            var hud=Object.FindFirstObjectByType<PlayerHud>(); var body=hud.GetComponent<BodyHudArt>();
            var panel=Object.FindFirstObjectByType<ChoicePanel>(); var run=Object.FindFirstObjectByType<RunController>();
            var interactor=Object.FindFirstObjectByType<PlayerInteractor>();
            Assert(Ref<MonoBehaviour>(hud,"stateSource")==state && Ref<MonoBehaviour>(body,"stateSource")==state,"HP and body read unique real PlayerState");
            var edge=hud.GetComponentInChildren<GlobalEdgeAnimator>(true);
            Assert(edge && edge.isActiveAndEnabled && !edge.transform.IsChildOf(panel.transform),"Gameplay edge has independent lifetime");
            Assert(UIFinalRevision.Refs<Sprite>(edge,"frames").Distinct().Count()==3,"Three actual edge sprites");
            var chests=Object.FindObjectsByType<Chest>(FindObjectsSortMode.None).OrderBy(c=>c.name).ToArray();
            var enemies=Object.FindObjectsByType<EnemyBasic>(FindObjectsSortMode.None);
            Assert(chests.Length==9 && enemies.Length==14,"Latest Ming 9 chests / 14 real enemies");
            foreach(var enemy in enemies) { report.AppendLine("Enemy "+enemy.name+" at "+enemy.transform.position); }
            report.AppendLine("Spawn "+state.transform.position+"; Start entered real Level.");
            Capture("Gameplay_Spawn",true);
            // Near-spawn chest is reached by real movement input, without repositioning.
            var spawn=state.transform.position; InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A));
            until=Time.unscaledTime+2;
            while(Time.unscaledTime<until && state.transform.position.x>-15.1f) { yield return null; }
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return Wait(.15f);
            Assert(state.transform.position.x<spawn.x-1,"A input moves player toward Chest_03");
            interactor.RefreshTarget();
            report.AppendLine("Walking target="+interactor.InteractionId+" pos="+state.transform.position);
            Assert(interactor.InteractionId=="whitebox-chest-03","Walking acquires actual Chest_03");
            double before=edge.Elapsed;
            yield return KeyPress(Key.E);
            Assert(panel.IsOpen && run.Phase==RunPhase.Choosing,"E -> input/interactor/Chest/Coordinator/ChoicePanel");
            yield return ObserveEdge(edge,"Choosing",4);
            var cards=panel.GetComponentsInChildren<CardVisual>(); Assert(cards.Length==3,"Real three choices");
            foreach(var c in cards) { CheckCard(c); Assert(c.Icon.sprite,"Real reward has icon "+RuntimeId(c)); }
            Capture("Chest03_ActualChoice",true);
            var first=EventSystem.current.currentSelectedGameObject;
            yield return KeyPress(Key.RightArrow);
            Assert(EventSystem.current.currentSelectedGameObject!=first,"Keyboard navigation preserved");
            var next=EventSystem.current.currentSelectedGameObject;
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.DpadLeft)); yield return Wait(.2f);
            InputSystem.QueueStateEvent(gamepad,new GamepadState()); yield return Wait(.2f);
            Assert(EventSystem.current.currentSelectedGameObject!=next,"Gamepad navigation preserved");
            int hp=state.CurrentHealth;
            yield return KeyPress(Key.Escape);
            Assert(!panel.IsOpen && run.Phase==RunPhase.Playing && state.CurrentHealth==hp,"Escape cancels real transaction without reward");
            Assert(edge.Elapsed>before+4 && edge.isActiveAndEnabled,"Same edge clock continues after cancel");
            yield return ObserveEdge(edge,"PlayingAfterCancel",2);
            report.AppendLine("Automated actual gameplay: MainMenu Start; walk A; E Chest03; keyboard/gamepad navigation; Esc. Human input remains for user acceptance.");

            var acquired=new HashSet<LoadoutItemId>();
            int bite=0,sword=0,fire=0;
            state.GetComponent<PlayerBiteAttack>().AttackStarted+=()=>bite++;
            state.GetComponent<PlayerSwordAttack>().AttackStarted+=()=>sword++;
            state.GetComponent<PlayerFireAttack>().AttackStarted+=()=>fire++;
            yield return KeyPress(Key.Enter); Assert(bite>0,"Real no-arms input dispatches Bite");
            // Position fixtures only for remote map coverage; reward pool and transaction stay real and unchanged.
            foreach(var chest in chests.OrderBy(c=>c.name=="Chest_03" ? 0 : 1))
            {
                MoveFixture(state,(Vector2)chest.transform.position+new Vector2(0,-.6f)); yield return Wait(.1f);
                interactor.RefreshTarget(); yield return KeyPress(Key.E);
                Assert(panel.IsOpen,"Remote position fixture: E opens "+chest.name+" target="+interactor.InteractionId);
                cards=panel.GetComponentsInChildren<CardVisual>();
                string[] desired={"arms","legs","tail","flame-tail"};
                LoadoutItemId[] items={LoadoutItemId.Arms,LoadoutItemId.Legs,LoadoutItemId.Tail,LoadoutItemId.FlameTail};
                CardVisual pick=null;
                for(int i=0;i<desired.Length;i++)
                { if(!acquired.Contains(items[i])) { pick=cards.FirstOrDefault(c=>RuntimeId(c)==desired[i]); if(pick) { break; } } }
                if(!pick) { pick=cards.FirstOrDefault(c=>RuntimeId(c)=="heal") ?? cards[0]; }
                string picked=RuntimeId(pick); CheckCard(pick); Click(pick.gameObject); yield return Wait(.2f);
                if(panel.IsOpen)
                {
                    var replace=panel.GetComponentsInChildren<CardVisual>();
                    report.AppendLine("Real replacement stage "+chest.name+": "+string.Join(",",replace.Select(RuntimeId)));
                    foreach(var c in replace) { CheckCard(c); }
                    Capture("Actual_Replacement",true); Click(replace[0].gameObject); yield return Wait(.2f);
                }
                Assert(chest.IsClaimed && !panel.IsOpen,"Real card click commits chest "+chest.name);
                foreach(var item in ((ILoadoutState)state).Items) { acquired.Add(item); }
                Assert(Ref<GameObject>(body,"arms").activeSelf==((ILoadoutState)state).Contains(LoadoutItemId.Arms),"Body arms follows receipt event");
                Assert(Ref<GameObject>(body,"legs").activeSelf==((ILoadoutState)state).Contains(LoadoutItemId.Legs),"Body legs follows receipt event");
                Assert(Ref<GameObject>(body,"flame").activeSelf==((ILoadoutState)state).Contains(LoadoutItemId.FlameTail),"Body flame follows receipt event");
                report.AppendLine("Chest receipt "+chest.name+": "+picked+" -> "+string.Join(",",((ILoadoutState)state).Items));
                yield return KeyPress(Key.Enter);
                if(((ILoadoutState)state).Contains(LoadoutItemId.FlameTail)) { yield return Wait(.6f); yield return KeyPress(Key.Q); }
                Capture("Receipt_"+chest.name,true);
            }
            report.AppendLine("Real chest acquired coverage="+string.Join(",",acquired)+"; attack events Bite="+bite+" Sword="+sword+" Fire="+fire);
            Assert(sword>0,"Real arms receipt enables Sword input");
            Assert(fire>0,"Real flame-tail receipt enables Q Fire");
            Assert(acquired.Count==4,"All four body types acquired via actual chest receipts");
            int healthEvents=0; ((IHealth)state).HealthChanged+=()=>healthEvents++;
            var target=enemies.Where(e=>e.IsAlive).OrderBy(e=>Vector2.Distance(e.transform.position,spawn)).First();
            hp=state.CurrentHealth; float oldFill=Ref<Image>(hud,"healthFill").fillAmount;
            MoveFixture(state,target.transform.position); until=Time.unscaledTime+3;
            while(Time.unscaledTime<until && state.CurrentHealth==hp) { yield return null; }
            Assert(state.CurrentHealth<hp && healthEvents>0,"Actual EnemyContactAttack damages PlayerState and raises HealthChanged");
            Assert(Ref<TMP_Text>(hud,"healthText").text==state.CurrentHealth.ToString() && Ref<Image>(hud,"healthFill").fillAmount<oldFill,"Real HP number and fill update together");
            report.AppendLine("Actual enemy contact fixture "+target.name+" "+target.transform.position+": HP "+hp+" -> "+state.CurrentHealth+"; HealthChanged="+healthEvents);
            Capture("Actual_Enemy_Damage",true);

            EditorSceneManager.LoadSceneInPlayMode(UIFinalRevision.ArtScene,new LoadSceneParameters(LoadSceneMode.Single));
            yield return Wait(.4f);
            var preview=Object.FindFirstObjectByType<UIArtPreview>(); preview.CardsPage();
            Ref<GameObject>(preview,"debugPanel").SetActive(false);
            var catalog=AssetDatabase.LoadAssetAtPath<CardPresentationCatalog>(UICardIconsRevision.CatalogPath);
            for(int i=0;i<20;i++)
            {
                preview.ShowCatalog(i); foreach(var c in UIFinalRevision.Refs<CardVisual>(preview,"cardVisuals")) { CheckCard(c); }
            }
            preview.NormalCopy(); Capture("ArtTest_Cards",false);
            yield return PixelOverlap(catalog);
            report.AppendLine("PASS. Visual tests are separate from gameplay input/state tests. No scene runtime fixtures saved.");
        }

        private static IEnumerator ObserveEdge(GlobalEdgeAnimator edge,string phase,float seconds)
        {
            double last=edge.Elapsed; int id=edge.GetInstanceID(); var frames=new HashSet<int>(); float end=Time.unscaledTime+seconds;
            while(Time.unscaledTime<end)
            {
                Assert(edge.Elapsed>=last && edge.GetInstanceID()==id && edge.isActiveAndEnabled,"Continuous edge timeline "+phase);
                last=edge.Elapsed; frames.Add(edge.FrameIndex);
                report.AppendLine("Edge "+phase+" t="+edge.Elapsed.ToString("F3")+" frame="+edge.FrameIndex+" timescale="+Time.timeScale);
                yield return Wait(.35f);
            }
            Assert(frames.Count>=2,"Actual edge frame changes "+phase);
        }

        private static void CheckCard(CardVisual card)
        {
            Assert(card.transform.Find("ContentArea/IconArea/Icon")==card.Art,"Shared IconArea hierarchy");
            Assert(card.transform.Find("ContentArea/DescriptionArea/DescriptionText")==card.DescriptionText.transform,"Shared DescriptionArea hierarchy");
            Assert(card.transform.Find("ContentArea/TitleArea/TitleBannerText")==card.Title.transform,"Shared TitleArea hierarchy");
            Assert(card.Title.fontSize==24 && card.DescriptionText.fontSize==22,"Title 24 / description 22");
            Assert(!card.Title.enableAutoSizing && !card.DescriptionText.enableAutoSizing,"No auto-sizing");
            Assert(card.GetComponentsInChildren<LayoutGroup>(true).Length==0 && card.GetComponentsInChildren<ContentSizeFitter>(true).Length==0,"No content-driven layout");
            card.Title.ForceMeshUpdate(); card.DescriptionText.ForceMeshUpdate();
            Assert(!card.Title.isTextOverflowing && !card.DescriptionText.isTextOverflowing,"Copy fits: "+card.Title.text+" / "+card.DescriptionText.text);
            Assert(card.Title.textInfo.lineCount<=2 && card.DescriptionText.textInfo.lineCount<=4,"Copy max 2/4 lines");
            Assert(AssetDatabase.GetAssetPath(card.transform.Find("Frame").GetComponent<Image>().sprite).EndsWith("UI_Card_Frame_Center.png"),"Only center frame");
        }

        private static string Geometry(CardVisual card)
        {
            return string.Join("\n",card.GetComponentsInChildren<RectTransform>(true).Where(t=>t!=card.transform).Select(t=>
                AnimationUtility.CalculateTransformPath(t,card.transform)+"|"+t.anchorMin.ToString("R")+t.anchorMax.ToString("R")+t.pivot.ToString("R")+
                t.anchoredPosition3D.ToString("R")+t.sizeDelta.ToString("R")+t.localScale.ToString("R")+t.localRotation.ToString("R")));
        }
        private static IEnumerator PixelOverlap(CardPresentationCatalog catalog)
        {
            foreach(var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) { c.gameObject.SetActive(false); }
            var root=new GameObject("VISUAL ONLY Pixel Overlap",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Choice/ChoiceCard.prefab");
            var cards=Enumerable.Range(0,3).Select(i=>Object.Instantiate(prefab,root.transform).GetComponent<CardVisual>()).ToArray();
            var sample=catalog.FindArtwork("GrowArms");
            for(int i=0;i<3;i++)
            {
                cards[i].ApplyVariant(i); cards[i].SetContent(sample.previewTitle,sample.previewDescription); cards[i].SetArtwork(sample.sprite);
                UIFinalRevision.Fixed((RectTransform)cards[i].transform,Vector2.zero,new Vector2(415,614));
                cards[i].SetSelected(false); cards[i].gameObject.SetActive(false);
            }
            string baseline=Geometry(cards[0]);
            Assert(cards.All(c=>Geometry(c)==baseline),"Every child RectTransform identical for all three clones");
            byte[] first=null;
            for(int i=0;i<3;i++)
            {
                cards[i].gameObject.SetActive(true); cards[i].ApplyVariant(i); cards[i].transform.localRotation=Quaternion.identity;
                Canvas.ForceUpdateCanvases(); var bytes=Capture("Overlap_Zero_"+i,false);
                if(first==null) { first=bytes; } else { Assert(first.SequenceEqual(bytes),"ZERO-ROTATION PIXEL IDENTICAL clone "+i); }
                cards[i].gameObject.SetActive(false);
            }
            for(int i=0;i<3;i++)
            {
                cards[i].gameObject.SetActive(true); cards[i].ApplyVariant(i);
                ((RectTransform)cards[i].transform).anchoredPosition=new Vector2((i-1)*500,0);
                Assert(Geometry(cards[i])==baseline,"Only root rotation/row position changes; every child unchanged");
            }
            Capture("Overlap_RotationOnly",false);
            report.AppendLine("Pixel proof: same formal prefab/content/position, root rotations 0/0/0: RGB bytes identical. Restored +4/0/-4; child RectTransforms remain identical.");
            Object.Destroy(root); yield return null;
        }

        internal static byte[] Capture(string name,bool world)
        {
            const int width=1920,height=1080;
            var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas && c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var cameraObject=new GameObject("UI QA Camera",typeof(Camera),typeof(UniversalAdditionalCameraData));
            var camera=cameraObject.GetComponent<Camera>();
            if(world && Camera.main) { camera.CopyFrom(Camera.main); camera.transform.SetPositionAndRotation(Camera.main.transform.position,Camera.main.transform.rotation); }
            else { camera.cullingMask=1<<5; camera.transform.position=new Vector3(1000,1000,-10); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.16f,.16f,.16f); }
            var rt=new RenderTexture(width,height,24); var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
            var old=RenderTexture.active; camera.targetTexture=rt;
            var transforms=canvases.SelectMany(c=>c.GetComponentsInChildren<Transform>(true)).ToArray();
            var layers=transforms.Select(t=>t.gameObject.layer).ToArray(); var scales=canvases.Select(c=>c.scaleFactor).ToArray();
            try
            {
                camera.cullingMask|=1<<5;
                foreach(var t in transforms) { t.gameObject.layer=5; }
                foreach(var canvas in canvases)
                {
                    canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
                    canvas.GetComponent<CanvasScaler>().enabled=false; canvas.scaleFactor=1;
                }
                Canvas.ForceUpdateCanvases();
                foreach(var f in Object.FindObjectsByType<UIBoardFit>(FindObjectsSortMode.None)) { f.Refresh(); }
                foreach(var p in Object.FindObjectsByType<UIArtPreview>(FindObjectsSortMode.None)) { p.Layout(); }
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=rt;
                texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply();
                File.WriteAllBytes(Output+name+".png",texture.EncodeToPNG());
                return texture.GetRawTextureData();
            }
            finally
            {
                RenderTexture.active=old;
                for(int i=0;i<canvases.Length;i++)
                { canvases[i].renderMode=RenderMode.ScreenSpaceOverlay; canvases[i].worldCamera=null; canvases[i].scaleFactor=scales[i]; canvases[i].GetComponent<CanvasScaler>().enabled=true; }
                for(int i=0;i<transforms.Length;i++) { transforms[i].gameObject.layer=layers[i]; }
                camera.targetTexture=null;
                Object.DestroyImmediate(texture); Object.DestroyImmediate(rt); Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
