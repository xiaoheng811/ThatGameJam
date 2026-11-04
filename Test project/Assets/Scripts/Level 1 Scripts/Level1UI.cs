using TMPro;
using UnityEngine;

namespace ThatGameJam.LevelOne
{
    public sealed class Level1UI : MonoBehaviour
    {
        public GameObject buttonGroup, gameplayHUD, settingPanel2, pausePanel, endingPanel, quitPanel;
        public UnityEngine.UI.Button playButton, settingsButton2, settingsCloseButton;
        public UnityEngine.UI.Button pauseButton, restartButton, resumeButton, pauseCloseButton;
        public UnityEngine.UI.Button pauseMainButton, endingMainButton, playagainButton, yesButton, noButton;
        public TMP_Text fragmentCounterText;
        public UnityEngine.UI.Graphic[] fragmentIcons = new UnityEngine.UI.Graphic[3];
        public UnityEngine.UI.Slider[] volumeSliders = new UnityEngine.UI.Slider[3];
        public Color collectedColor = new Color(.15f,.85f,.75f,1);
        public Color lockedColor = new Color(.8f,.8f,.8f,1);
        Level1Game game;
        Color[] iconDefaults;
        Color[][] sliderDefaults;
        UnityEngine.UI.Graphic[][] sliderGraphics;
        public bool BlocksGameplay => Visible(pausePanel) || Visible(settingPanel2) || Visible(quitPanel);
        public bool AudioPaused => Visible(pausePanel) || Visible(quitPanel);
        static bool Visible(GameObject o) => o && o.activeInHierarchy;

        public bool ValidateReferences()
        {
            bool ok=buttonGroup && gameplayHUD && settingPanel2 && pausePanel && endingPanel && quitPanel &&
                playButton && settingsButton2 && settingsCloseButton && pauseButton && restartButton &&
                pauseCloseButton && playagainButton && yesButton && noButton && fragmentCounterText &&
                fragmentIcons.Length==3 && volumeSliders.Length==3;
            if(ok) for(int i=0;i<3;i++) ok &= fragmentIcons[i] && volumeSliders[i];
            if(!ok) Debug.LogError("[Level1UI] Assign every required UI field and three icons/sliders. Resume/Main buttons are optional.",this);
            return ok;
        }
        public void Bind(Level1Game owner)
        {
            game=owner;
            iconDefaults=new Color[3]; sliderDefaults=new Color[3][];
            sliderGraphics=new UnityEngine.UI.Graphic[3][];
            for(int i=0;i<3;i++)
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
            Wire(yesButton,Quit);
            Wire(noButton,()=>quitPanel.SetActive(false));
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
            buttonGroup.SetActive(!playing); gameplayHUD.SetActive(playing);
            for(int i=0;i<3;i++) volumeSliders[i].SetValueWithoutNotify(1);
        }
        public void Refresh(int count,Level1Game.Note[] notes)
        {
            fragmentCounterText.text=count+"/3";
            for(int i=0;i<3;i++)
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
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
    }
}
