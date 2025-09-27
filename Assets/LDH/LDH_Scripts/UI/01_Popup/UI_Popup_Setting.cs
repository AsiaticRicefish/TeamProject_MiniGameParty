using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Data;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using LDH_Util;
using Managers;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LDH_UI
{
    public class UI_Popup_Setting : UI_Popup
    {
        [Header("Button")] [SerializeField] private Button closeButton;
        [SerializeField] private Button accountButton;
        [SerializeField] private TextMeshProUGUI accountButtonText;

        [Header("Button Style")] [SerializeField]
        private UI_ButtonStateStyle linkedStyle;

        [SerializeField] private UI_ButtonStateStyle unlinkedStyle;

        [Header("User Info")] [SerializeField] private TextMeshProUGUI linkAccount;
        [SerializeField] private TextMeshProUGUI uid;
        [SerializeField] private TextMeshProUGUI nickname;

        [Header("Sound")] [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;


        [Header("Version 정보")] [SerializeField]
        private TextMeshProUGUI versionText;


        [Header("Message")] [SerializeField] private string needToLinkAccount = "계정을 연결해주세요.";
        [SerializeField] private string versionFormat = "버전 정보 : {0}";


        private bool _wiring; // 슬라이더 값 주입 시 역발화 방지

        // ---------- Lifecycle ----------
        protected override void Init()
        {
            base.Init();
            Subscribe();

            SyncFromManagerToUI();
        }

        protected override void Clear()
        {
            base.Clear();
            Unsubscribe();
        }

        private void OnEnable()
        {
            //계정 정보 반영하기
            RefreshAllAccountUI();
            UpdateUID();
            UpdateNickName();

            // 버전 정보
            UpdateVersion();

            // 팝업 열릴 때마다 최신값으로 동기화
            SyncFromManagerToUI();
        }

        // ---------- Subscriptions ----------
        private void Subscribe()
        {
            if (closeButton) closeButton.onClick.AddListener(RequestClose);

            if (bgmSlider)
            {
                bgmSlider.minValue = 0f;
                bgmSlider.maxValue = 1f;
                bgmSlider.wholeNumbers = false;
                bgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);
            }

            if (sfxSlider)
            {
                sfxSlider.minValue = 0f;
                sfxSlider.maxValue = 1f;
                sfxSlider.wholeNumbers = false;
                sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
            }

            if (accountButton) accountButton.onClick.AddListener(OnClickAccountButton);
        }

        private void Unsubscribe()
        {
            if (closeButton) closeButton.onClick.RemoveAllListeners();
            if (bgmSlider) bgmSlider.onValueChanged.RemoveListener(OnBgmSliderChanged);
            if (sfxSlider) sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);
            if (accountButton) accountButton.onClick.RemoveListener(OnClickAccountButton);
        }


        #region 사운드

        private void SyncFromManagerToUI()
        {
            var sm = SoundManager.Instance;
            if (sm == null) return;

            _wiring = true;
            if (bgmSlider) bgmSlider.SetValueWithoutNotify(Mathf.Clamp01(sm.bgmSoundVolume));
            if (sfxSlider) sfxSlider.SetValueWithoutNotify(Mathf.Clamp01(sm.sfxSoundVolume));
            _wiring = false;
        }

        private void OnBgmSliderChanged(float v)
        {
            if (_wiring) return;
            SoundManager.Instance?.SetBGMSoundVolume(v); // → PlayerPrefs 저장 + Mixer 반영
        }

        private void OnSfxSliderChanged(float v)
        {
            if (_wiring) return;
            SoundManager.Instance?.SetSFXSoundVolume(v); // → PlayerPrefs 저장 + Mixer 반영
        }

        #endregion

        #region 계정 정보

        private void RefreshAllAccountUI()
        {
            UpdateLinkAccountSection();
            UpdateUID();
            UpdateNickName();
        }

        private void UpdateLinkAccountSection()
        {
            bool connected = false;
            try { connected = PlayGamesPlatform.Instance.localUser.authenticated; }
            catch { connected = false; }

            if (connected)
            {
                SetAccountButtonStyle(linked: true, interactable: false);
                var id = Social.localUser?.id ?? "";
                linkAccount.text = MaskId(id);
            }
            else
            {
                SetAccountButtonStyle(linked: false, interactable: true);
                linkAccount.text = needToLinkAccount;
            }
        }

        private void SetAccountButtonStyle(bool linked, bool interactable)
        {
            var style = linked ? linkedStyle : unlinkedStyle;
            if (accountButtonText)
            {
                accountButtonText.text = style.label;
                accountButtonText.color = style.labelColor;
            }

            if (accountButton) accountButton.interactable = interactable;
        }

        private void UpdateUID()
        {
#if TEST_WITHOUT_LOGIN
            uid.text = MaskId(PhotonNetwork.LocalPlayer.UserId);
#else
            uid.text = MaskId(Manager.Data.UID.Trim());
#endif
        }

        private void UpdateNickName()
        {
            nickname.text = (PhotonNetwork.LocalPlayer?.NickName ?? "").Trim();
        }


        private void OnClickAccountButton()
        {
#if UNITY_ANDROID && !UNITY_EDITOR

            if (!accountButton) return;
            accountButton.interactable = false;

            // 1) Authenticate
            PlayGamesPlatform.Instance.Authenticate(status =>
            {
                if (status != SignInStatus.Success)
                {
                    FinishAccountFlow(false, "계정 연결을 실패했습니다.");
                    return;
                }

                // 2) ServerAuthCode 우선
                TryRequestServerAuthCode(onOk: _ =>
                {
                        ApplyGpgsProfileToPhotonAndReleaseOld();
                    FinishAccountFlow(true, "계정 연결이 완료되었습니다.");
                }, onEmptyOrFail: () =>
                {
                    // 3) 폴백: IdToken
                    if (TryGetIdToken(out _))
                    {
                        ApplyGpgsProfileToPhotonAndReleaseOld();
                        FinishAccountFlow(true, "계정 연결이 완료되었습니다.");
                    }
                    else
                    {
                        FinishAccountFlow(false, "연결 실패: 인증 토큰을 가져오지 못했습니다.");
                    }
                });
            });
#else
            Manager.UI.EnqueueToast(Define_LDH.ToastType.Error, "Android 기기에서 테스트하세요.");
#endif
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        private void TryRequestServerAuthCode(Action<string> onOk, Action onEmptyOrFail)
        {
            try
            {
                PlayGamesPlatform.Instance.RequestServerSideAccess(false, code =>
                {
                    if (!string.IsNullOrEmpty(code)) onOk?.Invoke(code);
                    else onEmptyOrFail?.Invoke();
                });
            }
            catch { onEmptyOrFail?.Invoke(); }
        }

        private bool TryGetIdToken(out string idToken)
        {
            idToken = null;
            try
            {
                var active = (Social.Active as PlayGamesPlatform) ?? PlayGamesPlatform.Instance;
                var mi = active?.GetType().GetMethod("GetIdToken",
                    BindingFlags.Public | BindingFlags.Instance);
                if (mi != null) idToken = mi.Invoke(active, null) as string;
            }
            catch
            {
                /* no-op */
            }

            return !string.IsNullOrEmpty(idToken);
        }

        private void FinishAccountFlow(bool success, string msg)
        {
            RefreshAllAccountUI();
            Manager.UI.EnqueueToast(success ? Define_LDH.ToastType.Check : Define_LDH.ToastType.Error, msg);

            // 버튼 상태 업데이트(연결되면 비활성화)
            bool connected = false;
            try { connected = PlayGamesPlatform.Instance.localUser.authenticated; } catch { connected = false; }
            if (accountButton) accountButton.interactable = !connected;
        }
        
        private void ApplyGpgsProfileToPhotonAndReleaseOld()
        {
            // 1) 현재(적용 전) 내 닉네임을 old로 백업
            var oldName = (PhotonNetwork.LocalPlayer?.NickName ?? "").Trim();
            
            // 2) 레지스트리에서 이전 닉네임 반납 (같아도 무조건 시도)
            var uidStr = PhotonNetwork.LocalPlayer?.UserId ?? (Manager.Data?.UID ?? "");
             if (!string.IsNullOrWhiteSpace(uidStr) && !string.IsNullOrWhiteSpace(oldName))
            {
                UniTask.Void(async () =>
                {
                    try
                    {
                        bool released = await NicknameRegistry.ReleaseIfOwnerAsync(uidStr, oldName);
                        Debug.Log($"[Settings] Release old nickname '{oldName}' => {released}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Settings] Release old nickname failed: {e.Message}");
                    }
                });
            }
            // 3) GPGS 닉네임 적용 (비어있으면 기존 유지)
            var gpgsName = Social.localUser.userName;
            if (string.IsNullOrWhiteSpace(gpgsName)) gpgsName = PhotonNetwork.NickName;
            if (PhotonNetwork.LocalPlayer != null)
                PhotonNetwork.LocalPlayer.NickName = gpgsName;
            
            // RefreshAllAccountUI();
        }
        
        
        
#endif
        #endregion

        #region Version
        private void UpdateVersion()
        {
            if (!versionText) return;
            versionText.text = string.Format(versionFormat, Application.version);
        }

        #endregion


        static string MaskId(string v, int head = 3, int tail = 4)
        {
            if (string.IsNullOrEmpty(v) || v.Length <= head + tail) return v;
            return v.Substring(0, head) + new string('*', v.Length - head - tail) + v.Substring(v.Length - tail);
        }
    }
}