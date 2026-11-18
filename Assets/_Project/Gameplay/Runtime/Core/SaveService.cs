using UnityEngine;

namespace TomCatRunner.Gameplay.Core
{
    /// <summary>
    /// 存档服务 - PlayerPrefs 轻量持久化
    /// 最高分、累计金币、设置、教程完成标记
    /// </summary>
    public class SaveService
    {
        private const string KeyBestScore = "tcr_best_score";
        private const string KeyBestDistance = "tcr_best_distance";
        private const string KeyTotalCoins = "tcr_total_coins";
        private const string KeyGamesPlayed = "tcr_games_played";
        private const string KeyMusicOn = "tcr_music_on";
        private const string KeySfxOn = "tcr_sfx_on";
        private const string KeyTutorialShown = "tcr_tutorial_shown";

        public int BestScore { get; private set; }
        public float BestDistance { get; private set; }
        public int TotalCoins { get; private set; }
        public int GamesPlayed { get; private set; }
        public bool MusicOn { get; private set; }
        public bool SfxOn { get; private set; }
        public bool TutorialShown { get; private set; }

        public SaveService()
        {
            BestScore = PlayerPrefs.GetInt(KeyBestScore, 0);
            BestDistance = PlayerPrefs.GetFloat(KeyBestDistance, 0f);
            TotalCoins = PlayerPrefs.GetInt(KeyTotalCoins, 0);
            GamesPlayed = PlayerPrefs.GetInt(KeyGamesPlayed, 0);
            MusicOn = PlayerPrefs.GetInt(KeyMusicOn, 1) == 1;
            SfxOn = PlayerPrefs.GetInt(KeySfxOn, 1) == 1;
            TutorialShown = PlayerPrefs.GetInt(KeyTutorialShown, 0) == 1;
        }

        /// <summary>提交一局成绩，返回是否刷新纪录</summary>
        public bool SubmitRun(int score, float distance, int coins)
        {
            GamesPlayed++;
            TotalCoins += coins;
            PlayerPrefs.SetInt(KeyGamesPlayed, GamesPlayed);
            PlayerPrefs.SetInt(KeyTotalCoins, TotalCoins);

            bool newBest = false;
            if (score > BestScore)
            {
                BestScore = score;
                PlayerPrefs.SetInt(KeyBestScore, BestScore);
                newBest = true;
            }
            if (distance > BestDistance)
            {
                BestDistance = distance;
                PlayerPrefs.SetFloat(KeyBestDistance, BestDistance);
            }
            PlayerPrefs.Save();
            return newBest;
        }

        public void SetMusicOn(bool on)
        {
            MusicOn = on;
            PlayerPrefs.SetInt(KeyMusicOn, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void SetSfxOn(bool on)
        {
            SfxOn = on;
            PlayerPrefs.SetInt(KeySfxOn, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void MarkTutorialShown()
        {
            TutorialShown = true;
            PlayerPrefs.SetInt(KeyTutorialShown, 1);
            PlayerPrefs.Save();
        }
    }
}
