using FCSharpExt;
using FUnityCommon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using View;

namespace Ui
{
    public class RankItem : MonoBehaviour
    {
        public TMP_Text rankScore;
        public TMP_Text playerName;
        public Image avatar;
        public TMP_Text rank;
        private Sprite _defaultAvatar;

        private void Awake()
        {
            if (avatar != null)
            {
                _defaultAvatar = avatar.sprite;
            }
        }

        public void ResetDisplay(string defaultRank = "--", string defaultScore = "--", string defaultName = "--")
        {
            if (rank != null) rank.text = defaultRank;
            if (rankScore != null) rankScore.text = defaultScore;
            if (playerName != null) playerName.text = defaultName;
            if (avatar != null) avatar.sprite = _defaultAvatar;
        }

        public void UpdateInfo(RankingData data, string customScoreText = null)
        {
            if (avatar != null) avatar.sprite = _defaultAvatar;
            var user = data.user;
            if (rankScore != null)
            {
                rankScore.text = string.IsNullOrEmpty(customScoreText) ? data.score.ToThousands() : customScoreText;
            }
            if (rank != null) rank.text = data.rank.ToString();
            if (user == null)
            {
                if (playerName != null) playerName.text = "匿名用户";
                return;
            };
            if (playerName != null) playerName.text = user.nickname.IfEmpty("匿名用户");
            var avatarLink = user.avatar;
            if (string.IsNullOrEmpty(avatarLink)) return;
            FUnityNetworkImageLoader.LoadImage(avatarLink, icon =>
            {
                if (avatar != null) avatar.sprite = icon;
            });
        }
    }
}
