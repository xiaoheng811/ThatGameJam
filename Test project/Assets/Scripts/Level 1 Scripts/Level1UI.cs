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

        public TMP_Text fragmentCounterText;
        public UnityEngine.UI.Graphic[] fragmentIcons = new UnityEngine.UI.Graphic[3];
        public UnityEngine.UI.Slider[] volumeSliders = new UnityEngine.UI.Slider[3];
        public Color collectedColor = new Color(.15f,.85f,.75f,1);
        public Color lockedColor = new Color(.8f,.8f,.8f,1);
        Level1Game game;
        bool quitPending;
        bool nextLevelPending;
        Color[] iconDefaults;
        Color[][] sliderDefaults;
        UnityEngine.UI.Graphic[][] sliderGraphics;
        public bool BlocksGameplay => Visible(pausePanel) || Visible(settingPanel2) || Visible(quitPanel);
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
            Wire(playButton,game.Play);
            Wire(settingsButton2,()=>settingPanel2.SetActive(true));
            Wire(settingsCloseButton,()=>settingPanel2.SetActive(false));
            Wire(pauseButton,()=>pausePanel.SetActive(true));
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
        public void ResetView(bool playing)
        {
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
        }
        public void Refresh(int count,Level1Game.Note[] notes)
        {
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
            pausePanel.SetActive(false); settingPanel2.SetActive(false);
            endingPanel.SetActive(true);
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
            game.PlaySelectSound();
            StartCoroutine(LoadAfterSelect(sceneName, game.SelectSoundDuration));
        }

        IEnumerator LoadAfterSelect(string sceneName, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
            SceneManager.LoadScene(sceneName);
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








