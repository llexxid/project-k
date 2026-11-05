#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IdleKingdomFunction.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Scripts.Core.Manager;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace KingdomIdle.Editor
{
    public sealed class MailAdminWindow : EditorWindow
    {
        internal const string PendingKey = "KingdomIdle.MailAdmin.PendingRequest";
        private const string Server = "https://idlekingdomapis-azdgchdpa0g9grcm.koreasouth-01.azurewebsites.net/api/";
        [SerializeField] private Draft _draft = new();
        [NonSerialized] private string _functionKey, _adminKey;
        private string _pending;
        private VisualElement _form;
        private TextField _id, _output;
        private Button _send, _query, _newMail;
        private UnityWebRequest _request;

        [Serializable]
        internal sealed class Draft
        {
            public string Target = "", TitleKo = "우편 발송 테스트", BodyKo = "테스트 우편입니다. 첨부 보상을 수령해 주세요.";
            public string TitleEn = "Mail Delivery Test", BodyEn = "This is a test mail. Please claim the attached rewards.";
            public long Gold = 100;
            public int ValidDays = 1;

            internal static Draft Restore(string payload)
            {
                var body = JObject.Parse(payload);
                return new Draft
                {
                    Target = body["TargetPlayFabIds"][0].Value<string>(), Gold = body["Rewards"][0].Value<long>("Amount"), ValidDays = body.Value<int>("ValidDays"),
                    TitleKo = body["LocalizedTexts"]["ko"].Value<string>("Title"), BodyKo = body["LocalizedTexts"]["ko"].Value<string>("Body"),
                    TitleEn = body["LocalizedTexts"]["en"]?.Value<string>("Title") ?? "", BodyEn = body["LocalizedTexts"]["en"]?.Value<string>("Body") ?? ""
                };
            }

            internal string Build(string id, long now)
            {
                string target = Target?.Trim();
                if (string.IsNullOrEmpty(target) || !Regex.IsMatch(target, "^[A-Za-z0-9_.-]{1,64}\\z"))
                    throw new ArgumentException("게임에 접속한 테스트 계정의 PlayFabId를 입력해 주세요.");
                if (Gold <= 0 || ValidDays <= 0 || now + 3600 + (long)ValidDays * 86400 > 253402268399L)
                    throw new ArgumentException("골드는 양수, 유효기간은 서버에서 지원하는 양수 일수여야 합니다.");
                var texts = new JObject { ["ko"] = Text(TitleKo, BodyKo) };
                if (!string.IsNullOrWhiteSpace(TitleEn) || !string.IsNullOrWhiteSpace(BodyEn)) texts["en"] = Text(TitleEn, BodyEn);
                return JObject.FromObject(new
                {
                    ProtocolVersion = ApiVersions.ProtocolVersion, RequestId = id, Reason = "Unity 에디터 우편 전송 테스트",
                    TargetType = 1, TargetPlayFabIds = new[] { target }, DeliveryStartUtc = (long?)null,
                    DeliveryEndUtc = now + 3600, TextMode = 1, TitleKey = (string)null, BodyKey = (string)null,
                    DefaultLocale = "ko", LocalizedTexts = texts,
                    Rewards = new[] { new { Kind = 2, CurrencyType = 0, DefinitionId = (int?)null, Amount = Gold } }, ValidDays
                }).ToString(Formatting.None);
            }

            private static JObject Text(string title, string body)
            {
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body) ||
                    Encoding.UTF8.GetByteCount(title) > 256 || Encoding.UTF8.GetByteCount(body) > 2048)
                    throw new ArgumentException("언어별 제목·본문을 함께 입력해 주세요. 제목은 UTF-8 256바이트, 본문은 2,048바이트까지입니다.");
                return new JObject { ["Title"] = title, ["Body"] = body };
            }
        }

        [MenuItem("KingdomIdle/Mail/Send Test Mail")]
        public static void Open() => GetWindow<MailAdminWindow>("운영 우편 테스트").Show();

        public void CreateGUI()
        {
            minSize = new Vector2(520, 620);
            rootVisualElement.Clear();
            _pending = SessionState.GetString(PendingKey, "");
            if (!string.IsNullOrEmpty(_pending)) _draft = Draft.Restore(_pending);
            var scroll = new ScrollView(); scroll.style.flexGrow = 1;
            scroll.style.paddingLeft = scroll.style.paddingRight = 10;
            rootVisualElement.Add(scroll);
            scroll.Add(new HelpBox("IdleKingdomAPIs / PlayFab 145CAC 운영 서버\n지정 계정 한 명에게 실제 보상 우편을 등록합니다. 전달은 매분 처리합니다.", HelpBoxMessageType.Info));
            var auth = new Foldout { text = "인증 키 — 창을 닫으면 지워집니다", value = true };
            scroll.Add(auth);
            Field(auth, "Azure Function 키", _functionKey, v => _functionKey = v, password: true);
            Field(auth, "우편 관리자 키", _adminKey, v => _adminKey = v, password: true);
            auth.Add(new Label("Azure: 앱 키 → default / 환경 변수 → MAIL_ADMIN_KEY"));
            _form = new VisualElement(); scroll.Add(_form);
            var target = Field(_form, "대상 PlayFabId", _draft.Target, v => _draft.Target = v);
            _form.Add(new Button(() =>
            {
                var network = NetworkManager.Instance;
                if (network == null || !network.IsGameDataReady) { _output.value = "먼저 게임에 로그인하거나 PlayFabId를 직접 입력해 주세요."; return; }
                target.value = network.PlayFabId;
            }) { text = "현재 로그인한 계정 사용" });
            Field(_form, "한국어 제목", _draft.TitleKo, v => _draft.TitleKo = v);
            Field(_form, "한국어 본문", _draft.BodyKo, v => _draft.BodyKo = v, multiline: true);
            Field(_form, "영어 제목 (선택)", _draft.TitleEn, v => _draft.TitleEn = v);
            Field(_form, "영어 본문 (선택)", _draft.BodyEn, v => _draft.BodyEn = v, multiline: true);
            var gold = new LongField("첨부 골드") { value = _draft.Gold }; gold.RegisterValueChangedCallback(e => _draft.Gold = e.newValue); _form.Add(gold);
            var days = new IntegerField("전달 후 유효기간 (일)") { value = _draft.ValidDays }; days.RegisterValueChangedCallback(e => _draft.ValidDays = e.newValue); _form.Add(days);
            _id = new TextField("요청 ID") { isReadOnly = true }; scroll.Add(_id);
            var hint = new Label("등록 후 입력은 고정됩니다. 실패 시 같은 요청으로 재전송합니다. 발송 마감은 최초 준비 후 1시간입니다.");
            hint.style.whiteSpace = WhiteSpace.Normal; scroll.Add(hint);
            var actions = new VisualElement(); actions.style.flexDirection = FlexDirection.Row; scroll.Add(actions);
            _send = new Button(Send) { text = "우편 발송 등록" }; actions.Add(_send);
            _query = new Button(Query) { text = "발송 결과 조회" }; actions.Add(_query);
            _newMail = new Button(() => { _pending = ""; SessionState.EraseString(PendingKey); _output.value = "새 우편을 준비했습니다. 아직 발송하지 않았습니다."; UpdateControls(); }) { text = "새 테스트 우편" }; actions.Add(_newMail);
            _output = new TextField("처리 결과") { isReadOnly = true, multiline = true, value = string.IsNullOrEmpty(_pending)
                ? "아직 발송하지 않았습니다." : "이전 요청을 복원했습니다. 먼저 발송 결과를 조회해 주세요." };
            _output.style.minHeight = 150; scroll.Add(_output);
            UpdateControls();
        }

        private static TextField Field(VisualElement parent, string label, string value, Action<string> changed, bool multiline = false, bool password = false)
        {
            var field = new TextField(label) { value = value ?? "", multiline = multiline, isPasswordField = password };
            if (multiline) field.style.height = 65;
            field.RegisterValueChangedCallback(e => changed(e.newValue)); parent.Add(field); return field;
        }

        // shortcut: 재시도 본문은 현재 Editor 세션까지만 보관한다. 에디터 재시작 후 이력 조회가 필요하면 요청 ID 검색을 추가한다.
        internal static string Prepare(string pending, Draft draft, long now) => string.IsNullOrEmpty(pending)
            ? draft.Build("unity-mail-" + Guid.NewGuid().ToString("N"), now) : pending;

        private bool Authenticated()
        {
            if (ValidKey(_functionKey) && ValidKey(_adminKey)) return true;
            _output.value = "Azure Function 키와 우편 관리자 키를 입력해 주세요. 공백·줄바꿈이 들어간 키는 사용할 수 없습니다."; return false;
        }
        internal static bool ValidKey(string key) => !string.IsNullOrWhiteSpace(key) && key.Length <= 512 && key.All(c => c >= '!' && c <= '~');

        private void Send()
        {
            if (_request != null || !Authenticated()) return;
            try { _pending = Prepare(_pending, _draft, DateTimeOffset.UtcNow.ToUnixTimeSeconds()); }
            catch (ArgumentException e) { _output.value = e.Message; return; }
            SessionState.SetString(PendingKey, _pending);
            UpdateControls();
            Post("OnAdminSendMail", _pending);
        }
        private void Query()
        {
            if (_request != null || string.IsNullOrEmpty(_pending) || !Authenticated()) return;
            Post("OnAdminGetMailSendResult", new JObject { ["ProtocolVersion"] = ApiVersions.ProtocolVersion,
                ["RequestId"] = JObject.Parse(_pending).Value<string>("RequestId"), ["Count"] = 10, ["Cursor"] = null }.ToString(Formatting.None));
        }
        private void Post(string route, string body)
        {
            var request = new UnityWebRequest(Server + route, "POST") { uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(), timeout = 30, redirectLimit = 0 };
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            request.SetRequestHeader("x-functions-key", _functionKey);
            request.SetRequestHeader("X-Mail-Admin-Key", _adminKey);
            _request = request; _output.value = "요청 중입니다…"; UpdateControls();
            try { request.SendWebRequest().completed += _ => Complete(request, route); }
            catch (Exception)
            {
                _request = null; request.Dispose(); UpdateControls();
                _output.value = "요청을 시작하지 못했습니다. 요청 ID는 유지되며 같은 요청으로 다시 확인할 수 있습니다.";
            }
        }
        private void Complete(UnityWebRequest request, string route)
        {
            if (_request != request) return;
            _request = null;
            try { _output.value = Describe(request.responseCode, request.downloadHandler.text, _id.value, route == "OnAdminGetMailSendResult"); }
            catch (Exception) { _output.value = "응답을 확인하지 못했습니다. 새 우편을 만들기 전에 같은 요청의 발송 결과를 조회해 주세요."; }
            finally { request.Dispose(); UpdateControls(); }
        }
        internal static string Describe(long status, string json, string id, bool query)
        {
            if (status != 200) return "HTTP " + status + "\n" + (!string.IsNullOrEmpty(json) ? json :
                status == 0 ? "통신 실패 또는 제한시간 초과" : status == 401 ? "Azure Function 키와 우편 관리자 키를 확인해 주세요." : "서버가 요청을 거절했습니다.") +
                "\n기존 요청 ID는 유지됩니다. 결과를 조회하거나 같은 요청을 재전송하세요.";
            var body = JObject.Parse(json);
            if (body["ProtocolVersion"]?.Type != JTokenType.Integer || body.Value<int?>("ProtocolVersion") != ApiVersions.ProtocolVersion ||
                body["RequestId"]?.Type != JTokenType.String || body.Value<string>("RequestId") != id)
                throw new FormatException("Mail admin response mismatch.");
            if (!query)
            {
                if (body["AcceptedUtc"]?.Type != JTokenType.Integer || body.Value<long?>("AcceptedUtc") <= 0 || body["AlreadyRegistered"]?.Type != JTokenType.Boolean)
                    throw new FormatException("Mail registration response missing.");
                return "접수 성공 — 우편함 전달 완료는 아직 확인되지 않았습니다.\n약 1분 뒤 발송 결과를 조회해 주세요.";
            }
            var rows = body["Results"] as JArray ?? throw new FormatException("Mail result list missing.");
            if (rows.Count == 0) return "아직 전달 결과가 없습니다. 처리 전일 수 있으니 잠시 뒤 다시 조회해 주세요.";
            return string.Join("\n", rows.Select(row => row.Value<string>("PlayFabId") + ": " + (row.Value<int?>("Status") switch
            {
                1 => "전달 처리 중", 2 => "우편함 저장 완료 (보상 수령 여부는 별도)",
                3 => "미전달 — " + row.Value<string>("Message"),
                4 => "쓰기 결과 확인 필요 — 새 우편으로 재발송하지 마세요.", 5 => "발송 기간 종료",
                _ => throw new FormatException("Unknown mail delivery status.")
            })));
        }
        private void UpdateControls()
        {
            bool pending = !string.IsNullOrEmpty(_pending), busy = _request != null;
            _form.SetEnabled(!pending && !busy);
            _send.SetEnabled(!busy); _query.SetEnabled(pending && !busy); _newMail.SetEnabled(!busy);
            _send.text = pending ? "같은 요청 재전송" : "우편 발송 등록";
            _id.value = pending ? JObject.Parse(_pending).Value<string>("RequestId") : "발송 등록 시 생성";
        }
        private void OnDisable()
        {
            var request = _request; _request = null;
            if (request != null) { request.Abort(); request.Dispose(); }
            _functionKey = null; _adminKey = null;
        }
    }
}
#endif
