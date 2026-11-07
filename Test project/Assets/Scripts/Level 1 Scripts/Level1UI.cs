using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThatGameJam.LevelOne
{
    public sealed class Level1UI : MonoBehaviour
    {
        public GameObject buttonGroup, gameplayHUD, settingPanel2, pausePanel, endingPanel, quitPanel;
        public GameObject gameTitle;
        [Tooltip("The Audio Source used only on the main menu.")]
        public AudioSource menuMusic;
        public UnityEngine.UI.Button playButton, settingsButton2, settingsCloseButton;
        public UnityEngine.UI.Button pauseButton, restartButton, resumeButton, pauseCloseButton;
        public UnityEngine.UI.Button pauseMainButton, endingMainButton, playagainButton, yesButton, noButton;
        [Header("Next Level")]
        public UnityEngine.UI.Button nextLevelButton;
        public string nextSceneName;
        private TMP_Text nextButtonLabel;
        private string nextButtonOriginalText;
        private Color nextButtonOriginalColor;

        public TMP_Text fragmentCounterText;
        public UnityEngine.UI.Graphic[] fragmentIcons = new UnityEngine.UI.Graphic[3];
        public UnityEngine.UI.Slider[] volumeSliders = new UnityEngine.UI.Slider[3];
        public Color collectedColor = new Color(.15f,.85f,.75f,1);
        public Color lockedColor = new Color(.8f,.8f,.8f,1);

        [Header("First Level Tutorial")]
        [SerializeField] private GameObject newTutorialPanel;
        [SerializeField] private UnityEngine.UI.Button newTutorialCloseButton;

        [Header("Season Title")]
        [SerializeField] private UnityEngine.UI.Graphic seasonTitle;
        [SerializeField] private Color seasonWinColor = new Color(.15f, .85f, .75f, 1f);
        private Color seasonOriginalColor;

        [Header("Play Introduction")]
        [SerializeField] private UnityEngine.UI.Graphic introText;
        [SerializeField, Min(0f)] private float introSeconds = 2f;
        [SerializeField] private bool playIntroOnSceneEntry;
        public bool PlayIntroOnSceneEntry => playIntroOnSceneEntry;
        private Color introOriginalColor;
        private Coroutine introRoutine;

        Level1Game game;
        bool quitPending;
        bool restoreWinAfterSettings;
        bool nextLevelPending;
        Color[] iconDefaults;
        Color[][] sliderDefaults;
        UnityEngine.UI.Graphic[][] sliderGraphics;
        public bool BlocksGameplay =>
            Visible(pausePanel) || Visible(settingPanel2) || Visible(quitPanel) || Visible(newTutorialPanel);
        public bool AudioPaused => Visible(pausePanel) || Visible(quitPanel);
        static bool Visible(GameObject o) => o && o.activeInHierarchy;

        public bool ValidateReferences(int noteCount)
        {
            bool ok=buttonGroup && gameplayHUD && settingPanel2 && pausePanel && endingPanel && quitPanel &&
                playButton && settingsButton2 && settingsCloseButton && pauseButton && restartButton &&
                pauseCloseButton && (playagainButton || nextLevelButton) && yesButton && noButton;
            if (!ok)
            {
                Debug.LogError("[Level1UI] A required panel or button is missing.", this);
                return false;
            }
            if (fragmentIcons.Length != noteCount || volumeSliders.Length != noteCount)
            {
                Debug.LogError($"[Level1UI] Set Fragment Icons and Volume Sliders Size to {noteCount}.", this);
                return false;
            }
            for (int i = 0; i < noteCount; i++)
            {
                if (!fragmentIcons[i] || !volumeSliders[i])
                {
                    Debug.LogError($"[Level1UI] Assign the icon and slider at Element {i}.", this);
                    return false;
                }
                if (!fragmentIcons[i].transform.IsChildOf(gameplayHUD.transform))
                    Debug.LogWarning($"[Level1UI] Fragment Icons Element {i} is outside the assigned Gameplay HUD. Bind the icon in {gameplayHUD.name}.", this);
                if (!volumeSliders[i].transform.IsChildOf(settingPanel2.transform))
                    Debug.LogWarning($"[Level1UI] Volume Sliders Element {i} is outside the assigned Setting Panel2. Bind the slider in {settingPanel2.name}.", this);
            }
            return true;
        }
        public void Bind(Level1Game owner)
        {
            game=owner;
            if (nextLevelButton)
            {
                nextButtonLabel = nextLevelButton.GetComponentInChildren<TMP_Text>(true);
                if (nextButtonLabel)
                    nextButtonOriginalText = nextButtonLabel.text;
                if (nextLevelButton.targetGraphic)
                    nextButtonOriginalColor = nextLevelButton.targetGraphic.color;
            }
            if (introText)
                introOriginalColor = introText.color;
            if (seasonTitle)
                seasonOriginalColor = seasonTitle.color;
            int noteCount = fragmentIcons.Length;
            iconDefaults = new Color[noteCount];
            sliderDefaults = new Color[noteCount][];
            sliderGraphics = new UnityEngine.UI.Graphic[noteCount][];
            for (int i = 0; i < noteCount; i++)
            {
                iconDefaults[i]=fragmentIcons[i].color;
                sliderGraphics[i]=volumeSliders[i].GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                sliderDefaults[i]=new Color[sliderGraphics[i].Length];
                for(int j=0;j<sliderGraphics[i].Length;j++) sliderDefaults[i][j]=sliderGraphics[i][j].color;
                int index=i;
                volumeSliders[i].minValue=0; volumeSliders[i].maxValue=1; volumeSliders[i].wholeNumbers=false;
                volumeSliders[i].onValueChanged=new UnityEngine.UI.Slider.SliderEvent();
                volumeSliders[i].onValueChanged.AddListener(value=>game.SetVolume(index,value));
            }
            // Replace only these assigned buttons' events in memory. Prefab assets are untouched.
            Wire(playButton,BeginPlayIntroduction);
            if (newTutorialPanel && newTutorialCloseButton)
                Wire(newTutorialCloseButton, CloseStartTutorial);
            Wire(settingsButton2,OpenGameplaySettings);
            Wire(settingsCloseButton,CloseGameplaySettings);
            Wire(pauseButton,OpenGameplayPause);
            Wire(restartButton,game.Restart);
            Wire(resumeButton,()=>pausePanel.SetActive(false));
            Wire(pauseCloseButton,()=>pausePanel.SetActive(false));
            Wire(pauseMainButton,game.MainMenu);
            Wire(endingMainButton,game.MainMenu);
            Wire(playagainButton,game.Restart);
            Wire(nextLevelButton, LoadNextLevel);
            Wire(yesButton, Quit);
            // NoButton uses the Inspector events to restore the main menu.

            // Hook up every Button in the bound Canvas, including inactive panels.
            // Quit handles its own click sound so it can wait before exiting.
            var canvas = buttonGroup.GetComponentInParent<Canvas>(true);
            if (canvas)
                foreach (var button in canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    if (button != yesButton && button != nextLevelButton)
                        button.onClick.AddListener(game.PlaySelectSound);
        }
        static void Wire(UnityEngine.UI.Button button,UnityEngine.Events.UnityAction action)
        {
            if(!button) return;
            button.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
        }
        public void ShowSceneEntryIntroduction()
        {
            if (playIntroOnSceneEntry)
                BeginPlayIntroduction();
        }

        private void BeginPlayIntroduction()
        {
            if (introRoutine != null || game.Playing)
                return;

            if (!introText || introSeconds <= 0f)
            {
                game.Play();
                return;
            }

            buttonGroup.SetActive(false);
            if (gameTitle)
                gameTitle.SetActive(false);
            gameplayHUD.SetActive(false);
            if (newTutorialPanel)
                newTutorialPanel.SetActive(false);

            introText.color = introOriginalColor;
            introText.gameObject.SetActive(true);
            if (menuMusic && !menuMusic.isPlaying)
                menuMusic.Play();

            introRoutine = StartCoroutine(FadeIntroduction());
        }

        private IEnumerator FadeIntroduction()
        {
            float duration = introSeconds;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                Color color = introOriginalColor;
                color.a *= 1f - Mathf.Clamp01(elapsed / duration);
                introText.color = color;
            }

            introText.gameObject.SetActive(false);
            introText.color = introOriginalColor;
            introRoutine = null;
            game.Play();
        }

        public void ShowStartTutorial()
        {
            if (newTutorialPanel && newTutorialCloseButton)
            {
                pausePanel.SetActive(false);
                settingPanel2.SetActive(false);
                newTutorialPanel.SetActive(true);
            }
            RefreshHudButtons();
        }

        private void CloseStartTutorial()
        {
            if (newTutorialPanel)
                newTutorialPanel.SetActive(false);
            RefreshHudButtons();
        }

        private void RefreshHudButtons()
        {
            bool tutorialOpen = Visible(newTutorialPanel);
            if (pauseButton)
                pauseButton.interactable = !tutorialOpen &&
                    !Visible(settingPanel2) && !Visible(endingPanel);
            if (settingsButton2)
                settingsButton2.interactable = !tutorialOpen;
        }

        private void OpenGameplayPause()
        {
            if (Visible(newTutorialPanel) || Visible(settingPanel2) || Visible(endingPanel))
                return;

            pausePanel.SetActive(true);
        }
        private void OpenGameplaySettings()
        {
            if (Visible(newTutorialPanel) || Visible(settingPanel2))
                return;

            pausePanel.SetActive(false);
            restoreWinAfterSettings = Visible(endingPanel);
            if (restoreWinAfterSettings)
                endingPanel.SetActive(false);

            settingPanel2.SetActive(true);
            RefreshHudButtons();
        }

        private void CloseGameplaySettings()
        {
            settingPanel2.SetActive(false);
            bool restoreWin = restoreWinAfterSettings;
            restoreWinAfterSettings = false;
            if (restoreWin)
                endingPanel.SetActive(true);
            RefreshHudButtons();
        }

        public void ResetView(bool playing)
        {
            nextLevelPending = false;
            if (nextLevelButton)
            {
                nextLevelButton.interactable = true;
                if (nextButtonLabel)
                    nextButtonLabel.text = nextButtonOriginalText;
                if (nextLevelButton.targetGraphic)
                    nextLevelButton.targetGraphic.color = nextButtonOriginalColor;
            }
            restoreWinAfterSettings = false;
            if (introRoutine != null)
            {
                StopCoroutine(introRoutine);
                introRoutine = null;
            }
            if (introText)
            {
                introText.gameObject.SetActive(false);
                introText.color = introOriginalColor;
            }
            if (seasonTitle)
                seasonTitle.color = seasonOriginalColor;
            if (newTutorialPanel)
                newTutorialPanel.SetActive(false);
            settingPanel2.SetActive(false); pausePanel.SetActive(false);
            endingPanel.SetActive(false); quitPanel.SetActive(false);
            buttonGroup.SetActive(!playing);
            if (gameTitle) gameTitle.SetActive(!playing);
            gameplayHUD.SetActive(playing);
            if (menuMusic)
            {
                if (playing)
                    menuMusic.Stop();
                else if (!menuMusic.isPlaying)
                    menuMusic.Play();
            }
            foreach (var slider in volumeSliders) slider.SetValueWithoutNotify(1);
            RefreshHudButtons();
        }
        public void Refresh(int count,Level1Game.Note[] notes)
        {
            if (seasonTitle)
                seasonTitle.color = notes.Length > 0 && count == notes.Length
                    ? seasonWinColor
                    : seasonOriginalColor;
            if (fragmentCounterText) fragmentCounterText.text = $"{count}/{notes.Length}";
            for (int i = 0; i < notes.Length; i++)
            {
                bool unlocked=notes[i].collected;
                fragmentIcons[i].color=unlocked ? collectedColor : iconDefaults[i];
                volumeSliders[i].interactable=unlocked;
                for(int j=0;j<sliderGraphics[i].Length;j++)
                    sliderGraphics[i][j].color=unlocked ? sliderDefaults[i][j] :
                        new Color(lockedColor.r,lockedColor.g,lockedColor.b,sliderDefaults[i][j].a);
            }
        }
        public void ShowWin()
        {
            restoreWinAfterSettings = false;
            if (seasonTitle)
                seasonTitle.color = seasonWinColor;
            pausePanel.SetActive(false); settingPanel2.SetActive(false);
            endingPanel.SetActive(true);
            RefreshHudButtons();
        }
        void LoadNextLevel()
        {
            if (nextLevelPending) return;
            string sceneName = (nextSceneName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("[Level1UI] Set Next Scene Name and enable it in the Build Profiles Scene List: " + sceneName, this);
                return;
            }

            nextLevelPending = true;
            if (nextLevelButton)
            {
                nextLevelButton.interactable = false;
                if (nextButtonLabel)
                    nextButtonLabel.text = "Loading...";
                if (nextLevelButton.targetGraphic)
                {
                    Color dimmed = nextButtonOriginalColor;
                    dimmed.r *= .5f;
                    dimmed.g *= .5f;
                    dimmed.b *= .5f;
                    nextLevelButton.targetGraphic.color = dimmed;
                }
            }
            game.PlaySelectSound();
            StartCoroutine(LoadAfterSelect(sceneName, game.SelectSoundDuration));
        }

        IEnumerator LoadAfterSelect(string sceneName, float delay)
        {
            // Render the button feedback before beginning the scene load.
            yield return null;
            AsyncOperation loading = SceneManager.LoadSceneAsync(sceneName);
            if (loading == null)
            {
                Debug.LogError("[Level1UI] Unable to load scene: " + sceneName, this);
                nextLevelPending = false;
                if (nextLevelButton)
                {
                    nextLevelButton.interactable = true;
                    if (nextButtonLabel)
                        nextButtonLabel.text = nextButtonOriginalText;
                    if (nextLevelButton.targetGraphic)
                        nextLevelButton.targetGraphic.color = nextButtonOriginalColor;
                }
                yield break;
            }

            loading.allowSceneActivation = false;
            float elapsed = 0f;
            while (loading.progress < .9f || elapsed < delay)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            loading.allowSceneActivation = true;
            yield return loading;
        }

        public void Quit()
        {
            if (quitPending) return;
            quitPending = true;
            float delay = game ? game.SelectSoundDuration : 0f;
            if (game) game.PlaySelectSound();
            if (delay > 0f)
                StartCoroutine(QuitAfterSound(delay));
            else
                ExitGame();
        }

        IEnumerator QuitAfterSound(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            ExitGame();
        }

        static void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
    }
}








