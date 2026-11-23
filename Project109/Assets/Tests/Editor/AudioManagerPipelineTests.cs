using System;
using System.Collections.Generic;
using System.Reflection;
using CardTypes;
using GameItem.Types;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[TestFixture]
public class AudioManagerPipelineTests
{
    private sealed class SpyAudioService : IAudioService
    {
        public readonly List<string> playedSfxKeys = new List<string>();
        public readonly List<string> playedBgmKeys = new List<string>();

        public float MasterVolume => 1f;
        public float BgmVolume => 1f;
        public float SfxVolume => 1f;
        public string CurrentBgmKey { get; private set; } = string.Empty;

        public void PlayBGM(string key, float fadeDuration = 0.8f)
        {
            CurrentBgmKey = key;
            playedBgmKeys.Add(key);
        }

        public void PlayBGM(AudioClip clip, float fadeDuration = 0.8f, string clipKey = null)
        {
            CurrentBgmKey = clipKey ?? (clip != null ? clip.name : string.Empty);
            playedBgmKeys.Add(CurrentBgmKey);
        }

        public void StopBGM(float fadeDuration = 0.5f)
        {
            CurrentBgmKey = string.Empty;
        }

        public bool PlaySFX(string key, float pitchVariance = 0.05f, float volumeScale = 1f)
        {
            playedSfxKeys.Add(key);
            return true;
        }

        public bool PlaySFX(AudioClip clip, string throttleKey = null, float pitchVariance = 0.05f, float volumeScale = 1f)
        {
            playedSfxKeys.Add(throttleKey ?? (clip != null ? clip.name : string.Empty));
            return true;
        }
    }

    private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();
    private readonly List<Card> _createdCards = new List<Card>();
    private readonly List<Player> _createdPlayers = new List<Player>();

    private bool _hadMasterPref;
    private float _prevMasterPref;
    private bool _hadBgmPref;
    private float _prevBgmPref;
    private bool _hadSfxPref;
    private float _prevSfxPref;
    private float _prevAudioListenerVolume;

    [SetUp]
    public void SetUp()
    {
        _hadMasterPref = PlayerPrefs.HasKey(AudioManager.PREF_MASTER_VOLUME);
        _prevMasterPref = PlayerPrefs.GetFloat(AudioManager.PREF_MASTER_VOLUME, 1f);
        _hadBgmPref = PlayerPrefs.HasKey(AudioManager.PREF_BGM_VOLUME);
        _prevBgmPref = PlayerPrefs.GetFloat(AudioManager.PREF_BGM_VOLUME, 1f);
        _hadSfxPref = PlayerPrefs.HasKey(AudioManager.PREF_SFX_VOLUME);
        _prevSfxPref = PlayerPrefs.GetFloat(AudioManager.PREF_SFX_VOLUME, 1f);
        _prevAudioListenerVolume = AudioListener.volume;

        AudioManager.SetMasterVolume(1f, savePrefs: false);
        AudioManager.SetBgmVolume(1f, savePrefs: false);
        AudioManager.SetSfxVolume(1f, savePrefs: false);
        AudioServiceLocator.Register(null);
    }

    [TearDown]
    public void TearDown()
    {
        AudioServiceLocator.Register(null);

        for (int i = _createdCards.Count - 1; i >= 0; i--)
        {
            if (_createdCards[i] != null && !_createdCards[i].IsDisposed)
            {
                _createdCards[i].Dispose();
            }
        }
        _createdCards.Clear();

        for (int i = _createdPlayers.Count - 1; i >= 0; i--)
        {
            _createdPlayers[i]?.Dispose();
        }
        _createdPlayers.Clear();

        AudioManager existingAudio = UnityEngine.Object.FindFirstObjectByType<AudioManager>();
        if (existingAudio != null)
        {
            InvokePrivateMethod(existingAudio, "OnDestroy");
            UnityEngine.Object.DestroyImmediate(existingAudio.gameObject);
        }
        SetStaticField<AudioManager>("_instance", null);
        SetStaticField<AudioManager>("_isShuttingDown", false);

        PlayerInputController inputCtrl = UnityEngine.Object.FindFirstObjectByType<PlayerInputController>();
        if (inputCtrl != null)
        {
            UnityEngine.Object.DestroyImmediate(inputCtrl.gameObject);
        }
        SetStaticField<PlayerInputController>("_instance", null);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);

        SetStaticAutoProperty<RunManager>("instance", null);
        SetStaticAutoProperty<GameSceneManager>("instance", null);
        SetStaticAutoProperty<UIManager>("instance", null);

        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
            }
        }
        _createdObjects.Clear();

        RestoreFloatPref(AudioManager.PREF_MASTER_VOLUME, _hadMasterPref, _prevMasterPref);
        RestoreFloatPref(AudioManager.PREF_BGM_VOLUME, _hadBgmPref, _prevBgmPref);
        RestoreFloatPref(AudioManager.PREF_SFX_VOLUME, _hadSfxPref, _prevSfxPref);
        PlayerPrefs.Save();

        AudioManager.SetMasterVolume(_hadMasterPref ? _prevMasterPref : 1f, savePrefs: false);
        AudioManager.SetBgmVolume(_hadBgmPref ? _prevBgmPref : 1f, savePrefs: false);
        AudioManager.SetSfxVolume(_hadSfxPref ? _prevSfxPref : 1f, savePrefs: false);
        AudioListener.volume = _prevAudioListenerVolume;
    }

    private static void RestoreFloatPref(string key, bool hadKey, float value)
    {
        if (hadKey)
        {
            PlayerPrefs.SetFloat(key, value);
        }
        else
        {
            PlayerPrefs.DeleteKey(key);
        }
    }

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        if (obj != null)
        {
            _createdObjects.Add(obj);
        }
        return obj;
    }

    private AudioManager CreateTestAudioManager()
    {
        GameObject go = Track(new GameObject("TestAudioManager"));
        AudioManager manager = go.AddComponent<AudioManager>();
        InvokePrivateMethod(manager, "Awake");
        return manager;
    }

    private AudioClip CreateDummyClip(string name)
    {
        AudioClip clip = Track(AudioClip.Create(name, 4410, 1, 44100, false));
        return clip;
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, $"메서드 '{methodName}'를 {target.GetType().Name}에서 찾을 수 없습니다.");
        method.Invoke(target, args);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"필드 '{fieldName}'을(를) {target.GetType().Name}에서 찾을 수 없습니다.");
        return (T)field.GetValue(target);
    }

    private static void SetStaticField<T>(string fieldName, object value)
    {
        FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        field?.SetValue(null, value);
    }

    private static void SetStaticAutoProperty<T>(string propName, object value)
    {
        PropertyInfo prop = typeof(T).GetProperty(propName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        prop?.SetValue(null, value);
    }

    [Test]
    public void AudioManager_SfxPoolingAndCooldownThrottling_PreventsOverlapAndReusesFixedPool()
    {
        AudioManager manager = CreateTestAudioManager();
        AudioClip hitClip = CreateDummyClip("HitClip");
        AudioClip drawClip = CreateDummyClip("DrawClip");
        manager.RegisterClip(AudioCueKeys.SFX_COMBAT_HIT, hitClip);
        manager.RegisterClip(AudioCueKeys.SFX_CARD_DRAW, drawClip);

        float mockClock = 10f;
        manager.ClockProviderForTest = () => mockClock;

        int initialPoolCount = manager.SfxPoolCount;
        Assert.GreaterOrEqual(initialPoolCount, 4);

        // 1. 동일 SFX 연속 호출 시 쿨다운 내 중복 재생 차단
        Assert.IsTrue(manager.PlaySFX(AudioCueKeys.SFX_COMBAT_HIT, pitchVariance: 0.05f));
        Assert.IsFalse(manager.PlaySFX(AudioCueKeys.SFX_COMBAT_HIT, pitchVariance: 0.05f));

        // 2. 다른 키는 즉시 재생 허용
        Assert.IsTrue(manager.PlaySFX(AudioCueKeys.SFX_CARD_DRAW, pitchVariance: 0.05f));

        // 3. 쿨다운 경과 후에는 재재생 허용 및 고정 크기 풀 유지
        for (int i = 0; i < 30; i++)
        {
            mockClock += 0.1f;
            Assert.IsTrue(manager.PlaySFX(AudioCueKeys.SFX_COMBAT_HIT, pitchVariance: 0.06f));
        }

        Assert.AreEqual(initialPoolCount, manager.SfxPoolCount, "SFX 풀 크기는 초기 고정 크기를 초과하지 않아야 합니다.");

        AudioSource[] sfxPool = GetPrivateField<AudioSource[]>(manager, "_sfxPool");
        for (int i = 0; i < sfxPool.Length; i++)
        {
            Assert.GreaterOrEqual(sfxPool[i].pitch, 0.90f);
            Assert.LessOrEqual(sfxPool[i].pitch, 1.10f);
        }
    }

    [Test]
    public void AudioManager_BgmCrossfadeAndIdempotency_SwitchesTracksAndRespectsVolume()
    {
        AudioManager manager = CreateTestAudioManager();
        AudioClip titleClip = CreateDummyClip("TitleBgm");
        AudioClip battleClip = CreateDummyClip("BattleBgm");
        manager.RegisterClip(AudioCueKeys.BGM_TITLE, titleClip);
        manager.RegisterClip(AudioCueKeys.BGM_BATTLE_NORMAL, battleClip);

        AudioManager.SetBgmVolume(0.75f, savePrefs: false);

        // 1. 첫 BGM 재생
        manager.PlayBGM(AudioCueKeys.BGM_TITLE, fadeDuration: 0f);
        Assert.AreEqual(AudioCueKeys.BGM_TITLE, manager.CurrentBgmKey);
        Assert.AreSame(titleClip, manager.ActiveBgmClip);
        Assert.AreEqual(0.75f, manager.ActiveBgmVolume, 0.001f);
        Assert.IsFalse(manager.IsCrossfading);

        // 2. 동일 키 재호출 시 멱등 유지 (크로스페이드 재시작 없음)
        manager.PlayBGM(AudioCueKeys.BGM_TITLE, fadeDuration: 1.0f);
        Assert.IsFalse(manager.IsCrossfading);
        Assert.AreSame(titleClip, manager.ActiveBgmClip);

        // 3. 다른 트랙 전환 시 듀얼 덱 크로스페이드 수행
        manager.PlayBGM(AudioCueKeys.BGM_BATTLE_NORMAL, fadeDuration: 1.0f);
        Assert.AreEqual(AudioCueKeys.BGM_BATTLE_NORMAL, manager.CurrentBgmKey);
        Assert.AreSame(battleClip, manager.ActiveBgmClip);
        Assert.IsTrue(manager.IsCrossfading);

        manager.UpdateCrossfade(0.5f);
        Assert.IsTrue(manager.IsCrossfading);
        Assert.AreEqual(0.375f, manager.ActiveBgmVolume, 0.01f);

        manager.UpdateCrossfade(0.5f);
        Assert.IsFalse(manager.IsCrossfading);
        Assert.AreEqual(0.75f, manager.ActiveBgmVolume, 0.001f);

        // 4. StopBGM 시 키 초기화
        manager.StopBGM(0f);
        Assert.AreEqual(string.Empty, manager.CurrentBgmKey);
        Assert.AreEqual(0f, manager.ActiveBgmVolume, 0.001f);
    }

    [Test]
    public void AudioManager_VolumePersistenceAndPauseMenuPanelSliders_SyncCorrectly()
    {
        AudioManager manager = CreateTestAudioManager();

        GameObject pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/PauseMenuPanel.prefab");
        Assert.IsNotNull(pausePrefab, "PauseMenuPanel.prefab을 찾을 수 없습니다.");

        GameObject canvasGo = Track(new GameObject("AudioPauseCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        GameObject pauseGo = Track(UnityEngine.Object.Instantiate(pausePrefab, canvas.transform, false));
        PauseMenuPanel pausePanel = pauseGo.GetComponent<PauseMenuPanel>();
        Assert.IsNotNull(pausePanel);

        try
        {
            InvokePrivateMethod(pausePanel, "Awake");
            InvokePrivateMethod(pausePanel, "OnEnable");
            pausePanel.OnOpen();

            GameObject settingsGroup = GetPrivateField<GameObject>(pausePanel, "settingsGroup");
            Button settingsBackButton = GetPrivateField<Button>(pausePanel, "settingsBackButton");
            Slider masterSlider = GetPrivateField<Slider>(pausePanel, "masterVolumeSlider");
            Slider bgmSlider = GetPrivateField<Slider>(pausePanel, "bgmVolumeSlider");
            Slider sfxSlider = GetPrivateField<Slider>(pausePanel, "sfxVolumeSlider");
            TextMeshProUGUI bgmText = GetPrivateField<TextMeshProUGUI>(pausePanel, "bgmVolumeValueText");
            TextMeshProUGUI sfxText = GetPrivateField<TextMeshProUGUI>(pausePanel, "sfxVolumeValueText");

            Assert.IsNotNull(masterSlider);
            Assert.IsNotNull(bgmSlider);
            Assert.IsNotNull(sfxSlider);
            Assert.IsNotNull(bgmText);
            Assert.IsNotNull(sfxText);

            Transform tooltipRow = settingsGroup.transform.Find("TooltipRow");
            Assert.IsNotNull(tooltipRow);
            Assert.Less(tooltipRow.GetSiblingIndex(), settingsBackButton.transform.GetSiblingIndex());

            // 슬라이더 조작 -> AudioManager + PlayerPrefs 동기화
            masterSlider.value = 0.6f;
            bgmSlider.value = 0.4f;
            sfxSlider.value = 0.8f;

            Assert.AreEqual(0.6f, manager.MasterVolume, 0.001f);
            Assert.AreEqual(0.4f, manager.BgmVolume, 0.001f);
            Assert.AreEqual(0.8f, manager.SfxVolume, 0.001f);
            Assert.AreEqual(0.6f, AudioListener.volume, 0.001f);
            Assert.AreEqual("40%", bgmText.text);
            Assert.AreEqual("80%", sfxText.text);

            Assert.AreEqual(0.6f, PlayerPrefs.GetFloat(AudioManager.PREF_MASTER_VOLUME), 0.001f);
            Assert.AreEqual(0.4f, PlayerPrefs.GetFloat(AudioManager.PREF_BGM_VOLUME), 0.001f);
            Assert.AreEqual(0.8f, PlayerPrefs.GetFloat(AudioManager.PREF_SFX_VOLUME), 0.001f);
        }
        finally
        {
            InvokePrivateMethod(pausePanel, "OnDisable");
            InvokePrivateMethod(pausePanel, "OnDestroy");
        }
    }

    [Test]
    public void AudioPipeline_CombatAndGameplayHooks_DispatchExpectedSfxKeysViaServiceLocator()
    {
        AudioManager manager = CreateTestAudioManager();
        SpyAudioService spy = new SpyAudioService();
        AudioServiceLocator.Register(spy);

        // 1. 전투 피격 / 실드 / 회복 / 사망 이벤트 훅 검증
        GameObject charGo = Track(new GameObject("AudioTestChar"));
        Character target = charGo.AddComponent<Character>();
        InvokePrivateMethod(target, "Awake");
        target.InitializeStat(new CharacterStat
        {
            maxHealth = 10f,
            maxStamina = 10f,
            staminaRegenPerSecond = 0f,
            moveRange = 3
        });

        target.TakeShield(target, 5f);
        target.TakeDamage(target, 3f);
        target.TakeHeal(target, 2f);
        target.TakeDamage(target, 50f);

        CollectionAssert.Contains(spy.playedSfxKeys, AudioCueKeys.SFX_COMBAT_SHIELD);
        CollectionAssert.Contains(spy.playedSfxKeys, AudioCueKeys.SFX_COMBAT_HIT);
        CollectionAssert.Contains(spy.playedSfxKeys, AudioCueKeys.SFX_COMBAT_HEAL);
        CollectionAssert.Contains(spy.playedSfxKeys, AudioCueKeys.SFX_COMBAT_DEATH);

        // 2. 카드 드로우 SFX 훅 검증
        spy.playedSfxKeys.Clear();
        BattleDeck deck = new BattleDeck(null);
        CardData cardData = new CardData { cardName = "테스트베기", stamina = 1, targetType = "Self" };
        Card card = new Card(cardData, null, null, -1);
        _createdCards.Add(card);
        deck.drawPile.Add(card);
        deck.DrawCards(1);
        CollectionAssert.Contains(spy.playedSfxKeys, AudioCueKeys.SFX_CARD_DRAW);

        // 3. 상점 구매 SFX 훅 검증
        spy.playedSfxKeys.Clear();
        GameObject playerCharGo = Track(new GameObject("ShopBuyerChar"));
        Character buyerChar = playerCharGo.AddComponent<Character>();
        InvokePrivateMethod(buyerChar, "Awake");
        Player buyer = new Player(buyerChar);
        _createdPlayers.Add(buyer);
        buyer.playerStat.InGameCurrencyGold = 500;

        GameObject gsmGo = Track(new GameObject("AudioGameSceneManager"));
        GameSceneManager gsm = gsmGo.AddComponent<GameSceneManager>();
        gsm.player = buyer;
        SetStaticAutoProperty<GameSceneManager>("instance", gsm);

        GameObject shopGo = Track(new GameObject("TestShopNPC"));
        ShopNPC shop = shopGo.AddComponent<ShopNPC>();
        shop.cardEntries.Add(new ShopCardEntry { cardData = cardData, isSold = false });
        Assert.IsTrue(shop.TryPurchaseCard(0, out _));
        CollectionAssert.Contains(spy.playedSfxKeys, AudioCueKeys.SFX_SHOP_PURCHASE);

        // 4. 보상 수령 및 전투 승리/패배 SFX 훅 검증
        spy.playedSfxKeys.Clear();
        GameObject inputGo = Track(new GameObject("AudioTestInputController"));
        PlayerInputController inputCtrl = inputGo.AddComponent<PlayerInputController>();
        SetStaticField<PlayerInputController>("_instance", inputCtrl);
        SetStaticField<PlayerInputController>("_isShuttingDown", false);

        GameObject runGo = Track(new GameObject("AudioRunManager"));
        RunManager runManager = runGo.AddComponent<RunManager>();
        SetStaticAutoProperty<RunManager>("instance", runManager);

        runManager.AddSingleRewardToSession(RewardType.Gold, string.Empty, 30);
        string goldEntryId = runManager.pendingRewardSession.entries[0].entryId;
        Assert.IsTrue(runManager.TryClaimPendingReward(goldEntryId));
        CollectionAssert.Contains(spy.playedSfxKeys, AudioCueKeys.SFX_REWARD_CLAIM);

        InvokePrivateMethod(runManager, "HandleBattleWon");
        CollectionAssert.Contains(spy.playedSfxKeys, AudioCueKeys.SFX_BATTLE_VICTORY);

        InvokePrivateMethod(runManager, "HandleBattleLost");
        CollectionAssert.Contains(spy.playedSfxKeys, AudioCueKeys.SFX_BATTLE_DEFEAT);
    }
}
