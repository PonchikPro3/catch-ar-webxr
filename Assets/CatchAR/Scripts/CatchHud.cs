using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatchAR
{
    public sealed class CatchHud : MonoBehaviour
    {
        public GameObject menu, playHud, result, placement, countdown;
        public TMP_Text score, countdownText, resultScore, placementText, menuNotice;
        public TMP_Text elapsedText, resultHeading;
        public UnityEngine.UI.Slider timeSlider;
        public Graphic timerFill;
        public RectTransform timerRow, clockIcon, scoreRect, safeArea, menuStack, resultStack, placementStack, effects;
        public UnityEngine.UI.Button playButton, exitButton, replayButton, menuButton, placeButton;
        public Sprite touchBurst;
        public GameObject modes, scanHud;
        public RectTransform modeStack, scanControls, scanInfo;
        public TMP_Text scanStatus, scanCounts, scanSource, scanPauseCaption;
        public UnityEngine.UI.Slider scanProgress;
        public UnityEngine.UI.Button fallingModeButton, scanModeButton, modeBackButton, scanPauseButton, scanResetButton, scanBackButton;
        CatchGame game;
        int previousScore = -1, lastSecond = -1;
        float shownScore, scoreFrom, scoreAge = 1, tickAge = 1, noticeUntil;

        public void Bind(CatchGame owner)
        {
            game = owner;
            playButton.onClick.AddListener(game.ShowModes);
            fallingModeButton.onClick.AddListener(game.SelectFallingMode);
            scanModeButton.onClick.AddListener(game.SelectRoomScanMode);
            modeBackButton.onClick.AddListener(game.ReturnToMenu);
            scanPauseButton.onClick.AddListener(game.scanner.TogglePause);
            scanResetButton.onClick.AddListener(game.scanner.ResetScan);
            scanBackButton.onClick.AddListener(game.ReturnToMenu);
            exitButton.onClick.AddListener(game.ExitGame);
            replayButton.onClick.AddListener(game.Restart);
            menuButton.onClick.AddListener(game.ReturnToMenu);
            placeButton.onClick.AddListener(game.PlaceArena);
        }

        public void Refresh(float phaseTime)
        {
            if (game == null) return;
            bool playing = game.Phase == GamePhase.Playing;
            bool surface = game.IsSurfaceMode;
            menu.SetActive(game.Phase == GamePhase.Ready);
            modes.SetActive(game.Phase == GamePhase.ModeSelection);
            scanHud.SetActive(game.Phase == GamePhase.Scanning);
            if (game.Phase == GamePhase.Scanning) RefreshScan();
            playHud.SetActive(playing || game.Phase == GamePhase.Countdown);
            result.SetActive(game.Phase == GamePhase.Result);
            placement.SetActive(game.Phase == GamePhase.Placing);
            countdown.SetActive(game.Phase == GamePhase.Countdown);
            countdownText.text = Mathf.Max(1, 3 - Mathf.FloorToInt(phaseTime)).ToString();
            countdownText.transform.localScale = Vector3.one * (1 + .12f * Mathf.Exp(-8 * (phaseTime % 1)));
            if (previousScore != game.Score)
            {
                scoreFrom = shownScore;
                scoreAge = game.Score > previousScore && previousScore >= 0 ? 0 : 1;
                previousScore = game.Score;
            }
            scoreAge += Time.unscaledDeltaTime;
            shownScore = Mathf.Lerp(scoreFrom, game.Score, Mathf.SmoothStep(0, 1, scoreAge / .28f));
            score.SetText(Mathf.RoundToInt(shownScore).ToString() + (surface ? " / 10" : ""));
            elapsedText.gameObject.SetActive(surface);
            elapsedText.text = FormatElapsed(game.Elapsed);
            float punch = Mathf.Sin(Mathf.Min(1, scoreAge / .42f) * Mathf.PI) * Mathf.Exp(-scoreAge * 4);
            scoreRect.localScale = Vector3.one * (1 + .4f * punch);
            scoreRect.localRotation = Quaternion.Euler(0, 0, -7 * punch);
            score.color = Color.Lerp(WarmPalette.Cream, WarmPalette.Orange, punch * 2);
            float ratio = surface ? (float)game.Caught / CatchGame.SurfaceCatchGoal : Mathf.Clamp01(game.Remaining / game.roundDuration);
            timeSlider.SetValueWithoutNotify(ratio);
            int second = surface ? Mathf.FloorToInt(game.Elapsed) : Mathf.CeilToInt(game.Remaining);
            if (second != lastSecond) { lastSecond = second; tickAge = 0; }
            tickAge += Time.unscaledDeltaTime;
            bool urgent = playing && !surface && game.Remaining <= 10;
            float tick = playing ? Mathf.Exp(-tickAge * 9) : 0;
            float shake = urgent ? Mathf.Sin(Time.unscaledTime * 24) * 5 : Mathf.Sin(tickAge * 18) * tick * 9;
            clockIcon.localRotation = Quaternion.Euler(0, 0, shake);
            clockIcon.localScale = Vector3.one * (1 + tick * (urgent ? .16f : .07f));
            timerRow.localScale = Vector3.one * (1 + (urgent ? .018f * Mathf.Sin(Time.unscaledTime * 12) : .006f * tick));
            timerFill.color = urgent ? Color.Lerp(WarmPalette.Red, WarmPalette.Orange, .5f + .5f * Mathf.Sin(Time.unscaledTime * 8))
                : Color.Lerp(WarmPalette.Orange, WarmPalette.Peach, ratio);
            resultHeading.text = surface ? "10 объектов поймано!\nТвоё время" : "Время вышло";
            resultScore.text = surface ? FormatElapsed(game.Elapsed) : game.Score.ToString();
            menuNotice.gameObject.SetActive(Time.unscaledTime < noticeUntil);
        }

        public static string FormatElapsed(float seconds)
        {
            int hundredths = Mathf.FloorToInt(Mathf.Max(0, seconds) * 100);
            return (hundredths / 6000).ToString("00") + ":" + (hundredths / 100 % 60).ToString("00") + "." + (hundredths % 100).ToString("00");
        }

        void LateUpdate()
        {
            if (safeArea == null) return;
            float width = safeArea.rect.width;
            timerRow.sizeDelta = new Vector2(Mathf.Min(840, width - 64), 80);
            float buttonWidth = Mathf.Min(660, width * .82f);
            menuStack.sizeDelta = new Vector2(buttonWidth, buttonWidth * .82f);
            resultStack.sizeDelta = new Vector2(buttonWidth, buttonWidth * 1.12f);
            placementStack.sizeDelta = new Vector2(Mathf.Min(760, width * .9f), 340);
            modeStack.sizeDelta = new Vector2(buttonWidth, buttonWidth * 1.16f);
            scanControls.sizeDelta = new Vector2(Mathf.Min(800, width * .92f), 190);
            scanInfo.sizeDelta = new Vector2(Mathf.Min(840, width - 64), 160);
        }

        void RefreshScan()
        {
            var scan = game.scanner;
            if (scanProgress != null) scanProgress.SetValueWithoutNotify(scan.Progress);
            scanStatus.text = "Сканируй комнату вокруг себя";
            scanPauseCaption.text = scan.IsComplete ? "Скан завершён" : scan.IsPaused ? "Продолжить" : "Зафиксировать";
            scanPauseButton.interactable = !scan.IsComplete && (scan.IsPaused || (scan.Count > 0 && scan.IsTracking));
        }

        public void SetPlacement(bool found, bool fallbackAvailable)
        {
            placementText.text = found ? "Пол найден!\nРазмести здесь игровую область." : fallbackAvailable
                ? "Пол пока не найден.\nМожно разместить область на 1,5 м ниже телефона." : "Наведи камеру на пол\nи медленно двигай телефон.";
            placeButton.interactable = found || fallbackAvailable;
        }

        public void ShowMenuNotice(string text)
        {
            menuNotice.text = text;
            noticeUntil = Time.unscaledTime + 7;
        }

        public void ShowCatchEffect(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(effects, screenPoint, null, out Vector2 local);
            var go = new GameObject("ThreeStrokeCatch", typeof(RectTransform), typeof(CanvasGroup), typeof(UnityEngine.UI.Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(effects, false);
            rect.anchoredPosition = local;
            rect.sizeDelta = new Vector2(116, 164);
            rect.localRotation = Quaternion.Euler(0, 0, Random.Range(-22, 22));
            rect.localScale = Vector3.one * .22f;
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = touchBurst;
            image.color = WarmPalette.Cream;
            image.preserveAspect = true;
            image.raycastTarget = false;
            go.AddComponent<CatchTouchBurst>().opacity = go.GetComponent<CanvasGroup>();
        }
    }
}
