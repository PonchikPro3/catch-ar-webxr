using TMPro;
using UnityEngine;

namespace CatchAR
{
    public sealed class CatchHud : MonoBehaviour
    {
        public GameObject menu, playHud, result, placement, countdown;
        public TMP_Text score, timer, combo, status, flash, countdownText;
        public TMP_Text resultScore, resultDetails, bestText, placementText;
        public UnityEngine.UI.Image timeBar;
        public UnityEngine.UI.Button playButton, replayButton, menuButton, placeButton;
        CatchGame game;
        float flashUntil;
        public void Bind(CatchGame owner)
        {
            game = owner;
            playButton.onClick.AddListener(game.StartDemo);
            replayButton.onClick.AddListener(game.Restart);
            menuButton.onClick.AddListener(game.ReturnToMenu);
            placeButton.onClick.AddListener(game.PlaceArena);
        }

        public void Refresh(float phaseTime)
        {
            if (game == null) return;
            menu.SetActive(game.Phase == GamePhase.Ready);
            playHud.SetActive(game.Phase == GamePhase.Playing || game.Phase == GamePhase.Countdown);
            result.SetActive(game.Phase == GamePhase.Result);
            placement.SetActive(game.Phase == GamePhase.Placing);
            countdown.SetActive(game.Phase == GamePhase.Countdown);
            countdownText.text = Mathf.Max(1, 3 - Mathf.FloorToInt(phaseTime)).ToString();
            score.text = game.Score.ToString("000");
            timer.text = Mathf.CeilToInt(game.Remaining).ToString("00") + "с";
            timer.color = game.Remaining <= 10 ? new Color(1, .65f, .3f) : Color.white;
            combo.text = game.Combo > 1 ? $"СЕРИЯ ×{game.Combo}" : "ЛОВИ БЫСТРЕЕ — СОБИРАЙ СЕРИЮ";
            status.text = game.IsAR ? "●  AR · РЕАЛЬНОЕ ОКРУЖЕНИЕ" : "●  ДЕМО · УПРАВЛЕНИЕ МЫШЬЮ";
            timeBar.fillAmount = game.Remaining / game.roundDuration;
            flash.gameObject.SetActive(Time.unscaledTime < flashUntil && game.Phase == GamePhase.Playing);
            bestText.text = "ЛИЧНЫЙ РЕКОРД  " + game.Best;
            resultScore.text = game.Score.ToString();
            resultDetails.text = $"Поймано: {game.Caught}   ·   Упало: {game.Missed}\nЛичный рекорд: {game.Best}";
        }

        public void SetPlacement(bool found, bool fallbackAvailable)
        {
            placementText.text = found ? "ПОВЕРХНОСТЬ НАЙДЕНА\nКоснись экрана, чтобы начать" :
                fallbackAvailable ? "ПОЛ НЕ НАЙДЕН\nКоснись экрана — примем пол на 1,5 м ниже камеры" :
                "НАВЕДИ КАМЕРУ НА ПОЛ\nМедленно перемещай телефон";
            placeButton.interactable = found || fallbackAvailable;
        }

        public void Flash(string message, bool success)
        {
            flash.text = message;
            flash.color = success ? new Color(.48f, 1, .76f) : new Color(1, .74f, .4f);
            flashUntil = Time.unscaledTime + .7f;
        }
    }
}
