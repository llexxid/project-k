#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
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
        private string _pending, _historyNext, _resultNext, _knownId;
        private bool _knownStopped;
        private VisualElement _form, _players, _start, _text, _keys;
        private TextField _draftId, _id, _lookup, _preview, _output;
        private Button _send, _query, _newMail, _stop, _historyFirst, _historyMore, _resultMore;
        private ListView _historyView;
        private readonly List<JObject> _history = new();
        private UnityWebRequest _request;

        [Serializable]
        internal sealed class Draft
        {
            public bool AllPlayers, UseKeys;
            public string RequestId = "", Reason = "Unity 에디터 우편 발송", Target = "";
            public string StartKst = FormatKst(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            public string EndKst = FormatKst(DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds());
            public string TitleKey = "mail.cbt.title", BodyKey = "mail.cbt.body";
            public string TitleKo = "우편 발송 테스트", BodyKo = "테스트 우편입니다. 첨부 보상을 수령해 주세요.";
            public string TitleEn = "Mail Delivery Test", BodyEn = "This is a test mail. Please claim the attached rewards.";
            public long Gold = 100;
            public int ValidDays = 1;

            internal static Draft Restore(string payload)
            {
                var body = JObject.Parse(payload);
                var texts = body["LocalizedTexts"] as JObject;
                return new Draft
                {
                    RequestId = body.Value<string>("RequestId"), Reason = body.Value<string>("Reason") ?? "Unity 에디터 우편 발송",
                    AllPlayers = body.Value<int>("TargetType") == 2, UseKeys = body.Value<int>("TextMode") == 0,
                    Target = body["TargetPlayFabIds"] is JArray targets ? string.Join("\n", targets.Values<string>()) : "",
                    StartKst = body.Value<long?>("DeliveryStartUtc") is long start ? FormatKst(start) : "",
                    EndKst = FormatKst(body.Value<long>("DeliveryEndUtc")),
                    Gold = body["Rewards"][0].Value<long>("Amount"), ValidDays = body.Value<int?>("ValidDays") ?? 0,
                    TitleKey = body.Value<string>("TitleKey") ?? "", BodyKey = body.Value<string>("BodyKey") ?? "",
                    TitleKo = texts?["ko"]?.Value<string>("Title") ?? "", BodyKo = texts?["ko"]?.Value<string>("Body") ?? "",
                    TitleEn = texts?["en"]?.Value<string>("Title") ?? "", BodyEn = texts?["en"]?.Value<string>("Body") ?? ""
                };
            }

            internal string Build(string id, long now)
            {
                if (!ValidId(id) || string.IsNullOrWhiteSpace(Reason) || Encoding.UTF8.GetByteCount(Reason) > 1024)
                    throw new ArgumentException("요청 ID는 영숫자·-_. 1~64자, 사유는 UTF-8 1,024바이트 이내로 입력해 주세요.");
                string[] targets = null;
                if (!AllPlayers)
                {
                    targets = (Target ?? "").Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim().ToUpperInvariant()).Distinct().ToArray();
                    if (targets.Length < 1 || targets.Length > 100 || targets.Any(t => !ValidId(t)))
                        throw new ArgumentException("대상 PlayFabId를 1~100명 입력해 주세요. 줄바꿈 또는 쉼표로 구분합니다.");
                }
                long end = ParseKst(EndKst);
                long? start = AllPlayers ? ParseKst(StartKst) : null;
                if (end <= now || (start.HasValue && (start.Value < 0 || start.Value >= end)))
                    throw new ArgumentException("발송 종료는 현재보다 미래여야 하며 시작보다 늦어야 합니다.");
                if (Gold <= 0 || ValidDays < 0 || end + (long)(ValidDays == 0 ? 30 : ValidDays) * 86400 > 253402268399L)
                    throw new ArgumentException("골드는 양수, 유효기간은 0(기본 30일) 또는 지원 범위의 양수 일수여야 합니다.");
                JObject texts = null;
                if (UseKeys)
                {
                    if (string.IsNullOrWhiteSpace(TitleKey) || string.IsNullOrWhiteSpace(BodyKey) || TitleKey.Length > 100 || BodyKey.Length > 100)
                        throw new ArgumentException("제목·본문 키를 각각 100자 이내로 입력해 주세요.");
                }
                else
                {
                    texts = new JObject { ["ko"] = Text(TitleKo, BodyKo) };
                    if (!string.IsNullOrWhiteSpace(TitleEn) || !string.IsNullOrWhiteSpace(BodyEn)) texts["en"] = Text(TitleEn, BodyEn);
                }
                return JObject.FromObject(new
                {
                    ProtocolVersion = ApiVersions.ProtocolVersion, RequestId = id, Reason,
                    TargetType = AllPlayers ? 2 : 1, TargetPlayFabIds = targets, DeliveryStartUtc = start, DeliveryEndUtc = end,
                    TextMode = UseKeys ? 0 : 1, TitleKey = UseKeys ? TitleKey : null, BodyKey = UseKeys ? BodyKey : null,
                    DefaultLocale = UseKeys ? null : "ko", LocalizedTexts = texts,
                    Rewards = new[] { new { Kind = 2, CurrencyType = 0, DefinitionId = (int?)null, Amount = Gold } },
                    ValidDays = ValidDays == 0 ? (int?)null : ValidDays
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

        internal static bool ValidId(string value) => value != null && Regex.IsMatch(value, "^[A-Za-z0-9_.-]{1,64}\\z");
        internal static long ParseKst(string value)
        {
            if (!DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) || time.Year < 1970)
                throw new ArgumentException("한국 시간(KST)을 yyyy-MM-dd HH:mm:ss 형식으로 입력해 주세요.");
            return new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Unspecified), TimeSpan.FromHours(9)).ToUnixTimeSeconds();
        }
        internal static string FormatKst(long value) => DateTimeOffset.FromUnixTimeSeconds(value).ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        [MenuItem("KingdomIdle/Mail/Send Test Mail")]
        public static void Open() => GetWindow<MailAdminWindow>("우편 관리").Show();

        public void CreateGUI()
        {
            minSize = new Vector2(600, 700); rootVisualElement.Clear();
            _pending = SessionState.GetString(PendingKey, "");
            string restoreError = null;
            if (!string.IsNullOrEmpty(_pending))
            {
                try { _draft = Draft.Restore(_pending); }
                catch (Exception) { restoreError = "이전 요청 표시를 복원하지 못했습니다. 원본 요청은 보존했습니다. 이력에서 확인한 뒤 새 우편을 준비해 주세요."; }
            }
            var scroll = new ScrollView(); scroll.style.flexGrow = 1; scroll.style.paddingLeft = scroll.style.paddingRight = 10;
            rootVisualElement.Add(scroll);
            scroll.Add(new HelpBox("IdleKingdomAPIs / PlayFab 145CAC 운영 서버\n전체 발송은 기간 중 접속 후 확인·우편함 조회 시 계정당 한 번 전달합니다. 중지는 이미 전달된 우편·보상을 회수하지 않습니다.", HelpBoxMessageType.Info));
            var auth = new Foldout { text = "인증 키 — 창을 닫으면 지워집니다", value = true }; scroll.Add(auth);
            Field(auth, "Azure Function 키", _functionKey, v => _functionKey = v, password: true);
            Field(auth, "우편 관리자 키", _adminKey, v => _adminKey = v, password: true);
            auth.Add(new Label("Azure: 앱 키 → default / 환경 변수 → MAIL_ADMIN_KEY"));
            var compose = new Foldout { text = "새 우편 작성", value = true }; scroll.Add(compose);
            _form = new VisualElement(); compose.Add(_form);
            _draftId = Field(_form, "요청 ID (빈 값은 자동)", _draft.RequestId, v => _draft.RequestId = v);
            Field(_form, "발송 사유", _draft.Reason, v => _draft.Reason = v);
            var audience = new DropdownField("발송 대상", new List<string> { "지정 유저", "전체 유저" }, _draft.AllPlayers ? 1 : 0);
            audience.RegisterValueChangedCallback(_ => { _draft.AllPlayers = audience.index == 1; RefreshModes(); }); _form.Add(audience);
            _players = new VisualElement(); _form.Add(_players);
            var target = Field(_players, "PlayFabId (줄바꿈 구분)", _draft.Target, v => _draft.Target = v, multiline: true);
            _players.Add(new Button(() =>
            {
                var network = NetworkManager.Instance;
                if (network == null || !network.IsGameDataReady) { _output.value = "먼저 게임에 로그인하거나 PlayFabId를 직접 입력해 주세요."; return; }
                target.value = network.PlayFabId;
            }) { text = "현재 로그인한 계정 사용" });
            _start = new VisualElement(); _form.Add(_start);
            Field(_start, "발송 시작 (KST)", _draft.StartKst, v => _draft.StartKst = v);
            Field(_form, "발송 종료 (KST)", _draft.EndKst, v => _draft.EndKst = v);
            _form.Add(new Label("시간 형식: yyyy-MM-dd HH:mm:ss / 지정 발송은 접수 후 종료 시각까지 처리"));
            var mode = new DropdownField("문구 방식", new List<string> { "Text — 언어별 원문", "Key — 클라이언트 사전" }, _draft.UseKeys ? 1 : 0);
            mode.RegisterValueChangedCallback(_ => { _draft.UseKeys = mode.index == 1; RefreshModes(); }); _form.Add(mode);
            _keys = new VisualElement(); _form.Add(_keys);
            var titleKey = Field(_keys, "제목 키", _draft.TitleKey, v => _draft.TitleKey = v);
            var bodyKey = Field(_keys, "본문 키", _draft.BodyKey, v => _draft.BodyKey = v);
            _keys.Add(new Button(() => { titleKey.value = "mail.cbt.title"; bodyKey.value = "mail.cbt.body"; }) { text = "CBT 문구 키 입력" });
            _text = new VisualElement(); _form.Add(_text);
            Field(_text, "한국어 제목", _draft.TitleKo, v => _draft.TitleKo = v);
            Field(_text, "한국어 본문", _draft.BodyKo, v => _draft.BodyKo = v, multiline: true);
            Field(_text, "영어 제목 (선택)", _draft.TitleEn, v => _draft.TitleEn = v);
            Field(_text, "영어 본문 (선택)", _draft.BodyEn, v => _draft.BodyEn = v, multiline: true);
            var gold = new LongField("첨부 골드") { value = _draft.Gold }; gold.RegisterValueChangedCallback(e => _draft.Gold = e.newValue); _form.Add(gold);
            var days = new IntegerField("유효일수 (0=기본 30일)") { value = _draft.ValidDays }; days.RegisterValueChangedCallback(e => _draft.ValidDays = e.newValue); _form.Add(days);
            _id = new TextField("고정된 발송 요청 ID") { isReadOnly = true }; compose.Add(_id);
            var actions = Row(compose);
            _send = new Button(Send) { text = "우편 발송 등록" }; actions.Add(_send);
            _newMail = new Button(NewMail) { text = "새 우편 준비" }; actions.Add(_newMail);
            var history = new Foldout { text = "발송 이력 — 요청 ID 순 / 페이지당 50건", value = true }; scroll.Add(history);
            var historyActions = Row(history);
            _historyFirst = new Button(() => LoadHistory(null)) { text = "이력 새로 고침" }; historyActions.Add(_historyFirst);
            _historyMore = new Button(() => LoadHistory(_historyNext)) { text = "다음 이력 페이지" }; historyActions.Add(_historyMore);
            _historyView = new ListView { itemsSource = _history, fixedItemHeight = 56, selectionType = SelectionType.Single,
                makeItem = () => new Label(), bindItem = (element, index) => ((Label)element).text = HistoryLabel(_history[index]) };
            _historyView.style.height = 180;
            _historyView.selectionChanged += selection => { if (_request == null && selection.FirstOrDefault() is JObject item) SelectHistory(item); };
            history.Add(_historyView);
            _lookup = Field(history, "조회·중지할 요청 ID", "", _ =>
            {
                _resultNext = null; _knownId = null; _knownStopped = false;
                _preview?.SetValueWithoutNotify(""); _historyView.ClearSelection(); UpdateControls();
            });
            var lookupActions = Row(history);
            _query = new Button(() => Query(null)) { text = "전달 결과 조회" }; lookupActions.Add(_query);
            _resultMore = new Button(() => Query(_resultNext)) { text = "다음 전달 결과" }; lookupActions.Add(_resultMore);
            _stop = new Button(Stop) { text = "선택한 발송의 신규 전달 중지" }; lookupActions.Add(_stop);
            history.Add(new HelpBox("이력을 선택하면 내용과 전달 결과를 확인합니다. 중지는 이후 새 전달만 막으며 이미 진행 중인 전달은 완료될 수 있습니다. 결과 행 수는 전체 유저 수·수령 완료 수가 아닙니다.", HelpBoxMessageType.Info));
            _preview = new TextField("선택한 발송 내용") { multiline = true, isReadOnly = true }; _preview.style.height = 130; history.Add(_preview);
            _output = new TextField("처리 결과") { isReadOnly = true, multiline = true, value = restoreError ??
                (string.IsNullOrEmpty(_pending) ? "이력을 새로 고치거나 새 우편을 작성해 주세요." : "이전 고정 요청을 복원했습니다. 이력에서 결과를 확인해 주세요.") };
            _output.style.minHeight = 150; scroll.Add(_output);
            RefreshModes(); UpdateControls();
        }

        private static VisualElement Row(VisualElement parent)
        { var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap; parent.Add(row); return row; }
        private static TextField Field(VisualElement parent, string label, string value, Action<string> changed, bool multiline = false, bool password = false)
        {
            var field = new TextField(label) { value = value ?? "", multiline = multiline, isPasswordField = password };
            if (multiline) field.style.height = 65;
            field.RegisterValueChangedCallback(e => changed(e.newValue)); parent.Add(field); return field;
        }
        private void RefreshModes()
        {
            _players.style.display = _draft.AllPlayers ? DisplayStyle.None : DisplayStyle.Flex;
            _start.style.display = _draft.AllPlayers ? DisplayStyle.Flex : DisplayStyle.None;
            _keys.style.display = _draft.UseKeys ? DisplayStyle.Flex : DisplayStyle.None;
            _text.style.display = _draft.UseKeys ? DisplayStyle.None : DisplayStyle.Flex;
            UpdateControls();
        }
        // shortcut: 고정 본문은 현재 Editor 세션까지만 보관한다. 재시작 뒤에는 서버 이력의 요청 ID로 조회·중지한다.
        internal static string Prepare(string pending, Draft draft, long now) => string.IsNullOrEmpty(pending)
            ? draft.Build(string.IsNullOrWhiteSpace(draft.RequestId) ? "unity-mail-" + Guid.NewGuid().ToString("N") : draft.RequestId.Trim(), now) : pending;
        private bool Authenticated()
        {
            if (ValidKey(_functionKey) && ValidKey(_adminKey)) return true;
            _output.value = "Azure Function 키와 우편 관리자 키를 입력해 주세요. 공백·줄바꿈이 들어간 키는 사용할 수 없습니다."; return false;
        }
        internal static bool ValidKey(string key) => !string.IsNullOrWhiteSpace(key) && key.Length <= 512 && key.All(c => c >= '!' && c <= '~');
        private void NewMail()
        {
            if (_request != null) return;
            _pending = ""; SessionState.EraseString(PendingKey); _draftId.value = "";
            _output.value = "새 우편을 준비했습니다. 요청 ID는 초기화했으며 아직 발송하지 않았습니다."; UpdateControls();
        }
        private void Send()
        {
            if (_request != null || !Authenticated()) return;
            try { _pending = Prepare(_pending, _draft, DateTimeOffset.UtcNow.ToUnixTimeSeconds()); }
            catch (ArgumentException e) { _output.value = e.Message; return; }
            SessionState.SetString(PendingKey, _pending); UpdateControls(); Post("OnAdminSendMail", _pending);
        }
        private void LoadHistory(string cursor)
        {
            if (_request != null || !Authenticated()) return;
            Post("OnAdminGetMailSendHistory", new JObject { ["ProtocolVersion"] = ApiVersions.ProtocolVersion, ["Count"] = 50, ["Cursor"] = cursor }.ToString(Formatting.None));
        }
        private void SelectHistory(JObject item)
        {
            _lookup.value = item["Request"].Value<string>("RequestId");
            int index = _history.IndexOf(item);
            if (index >= 0) _historyView.SetSelectionWithoutNotify(new[] { index });
            _preview.value = item["Request"].ToString(Formatting.Indented);
            _knownId = _lookup.value; _knownStopped = item.Value<long?>("StoppedUtc").HasValue;
            UpdateControls(); Query(null);
        }
        private void Query(string cursor)
        {
            if (_request != null || !ValidId(_lookup.value.Trim()) || !Authenticated()) return;
            if (cursor == null) _resultNext = null;
            Post("OnAdminGetMailSendResult", new JObject { ["ProtocolVersion"] = ApiVersions.ProtocolVersion,
                ["RequestId"] = _lookup.value.Trim(), ["Count"] = 50, ["Cursor"] = cursor }.ToString(Formatting.None));
        }
        private void Stop()
        {
            if (_request != null || _knownId != _lookup.value.Trim() || _knownStopped || !Authenticated()) return;
            Post("OnAdminStopMail", new JObject { ["ProtocolVersion"] = ApiVersions.ProtocolVersion, ["RequestId"] = _knownId }.ToString(Formatting.None));
        }
        private void Post(string route, string body)
        {
            string expectedId = JObject.Parse(body).Value<string>("RequestId");
            var request = new UnityWebRequest(Server + route, "POST") { uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(), timeout = 30, redirectLimit = 0 };
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            request.SetRequestHeader("x-functions-key", _functionKey); request.SetRequestHeader("X-Mail-Admin-Key", _adminKey);
            _request = request; _output.value = "요청 중입니다…"; UpdateControls();
            try { request.SendWebRequest().completed += _ => Complete(request, route, expectedId); }
            catch (Exception) { _request = null; request.Dispose(); UpdateControls(); _output.value = "요청을 시작하지 못했습니다. 기존 요청 ID는 유지됩니다."; }
        }
        private void Complete(UnityWebRequest request, string route, string expectedId)
        {
            if (_request != request) return;
            _request = null;
            try
            {
                if (request.responseCode != 200) { _output.value = Describe(request.responseCode, request.downloadHandler.text, expectedId, false); return; }
                var body = ReadResponse(request.downloadHandler.text, expectedId);
                if (route == "OnAdminGetMailSendHistory")
                {
                    var items = ReadHistory(body); _history.Clear(); _history.AddRange(items);
                    _historyNext = ReadCursor(body); _historyView.ClearSelection(); _historyView.Rebuild();
                    _output.value = $"이력 {items.Count}건을 읽었습니다. 항목을 선택해 내용을 확인해 주세요.";
                }
                else if (route == "OnAdminStopMail")
                {
                    if (body["StoppedUtc"]?.Type != JTokenType.Integer || body.Value<long>("StoppedUtc") <= 0 || body["AlreadyStopped"]?.Type != JTokenType.Boolean)
                        throw new FormatException("Stop acknowledgement missing.");
                    _knownId = expectedId; _knownStopped = true;
                    foreach (var item in _history) if (item["Request"].Value<string>("RequestId") == expectedId) item["StoppedUtc"] = body["StoppedUtc"].DeepClone();
                    _historyView.RefreshItems();
                    _output.value = "발송 중지를 확인했습니다. 이미 전달된 우편·보상은 유지됩니다. 중지 전에 진행 중인 전달은 완료될 수 있습니다.";
                }
                else if (route == "OnAdminGetMailSendResult")
                {
                    _output.value = Describe(200, request.downloadHandler.text, expectedId, true);
                    _resultNext = ReadCursor(body); _knownId = body.ContainsKey("StoppedUtc") ? expectedId : null;
                    _knownStopped = body.Value<long?>("StoppedUtc").HasValue;
                    if (string.IsNullOrEmpty(_preview.value))
                        _preview.value = new JObject { ["RequestId"] = expectedId, ["Reason"] = body["Reason"]?.DeepClone(),
                            ["TargetType"] = body["TargetType"]?.DeepClone(), ["DeliveryStartUtc"] = body["DeliveryStartUtc"]?.DeepClone(),
                            ["DeliveryEndUtc"] = body["DeliveryEndUtc"]?.DeepClone() }.ToString(Formatting.Indented);
                }
                else
                {
                    _output.value = Describe(200, request.downloadHandler.text, expectedId, false);
                    _lookup.value = expectedId;
                }
            }
            catch (Exception) { _output.value = "응답을 확인하지 못했습니다. 새 발송을 만들기 전에 이력에서 기존 요청을 확인해 주세요."; }
            finally { request.Dispose(); UpdateControls(); }
        }
        internal static JObject ReadResponse(string json, string id)
        {
            var body = JObject.Parse(json);
            if (body["ProtocolVersion"]?.Type != JTokenType.Integer || body.Value<int>("ProtocolVersion") != ApiVersions.ProtocolVersion ||
                (id != null && (body["RequestId"]?.Type != JTokenType.String || body.Value<string>("RequestId") != id)))
                throw new FormatException("Mail admin response mismatch.");
            return body;
        }
        internal static string ReadCursor(JObject body)
        {
            var cursor = body["NextCursor"];
            if (cursor == null || cursor.Type == JTokenType.Null) return null;
            if (cursor.Type != JTokenType.String || string.IsNullOrEmpty(cursor.Value<string>()) || cursor.Value<string>().Length > 8192)
                throw new FormatException("Invalid paging cursor.");
            return cursor.Value<string>();
        }
        internal static List<JObject> ReadHistory(JObject body)
        {
            var rows = body["HistoryItems"] as JArray ?? throw new FormatException("History missing.");
            if (rows.Count > 50) throw new FormatException("History page too large.");
            var result = new List<JObject>();
            foreach (var row in rows)
            {
                if (row is not JObject item || !ValidId(item["Request"]?.Value<string>("RequestId")) || item["AcceptedUtc"]?.Type != JTokenType.Integer || item.Value<long>("AcceptedUtc") <= 0 ||
                    item.Value<long>("AcceptedUtc") > 253402268399L || item["Request"].Value<int?>("TargetType") is not (1 or 2)) throw new FormatException("History item invalid.");
                if (item["StoppedUtc"] != null && item["StoppedUtc"].Type != JTokenType.Null &&
                    (item["StoppedUtc"].Type != JTokenType.Integer || item.Value<long>("StoppedUtc") <= 0 || item.Value<long>("StoppedUtc") > 253402268399L))
                    throw new FormatException("History stop time invalid.");
                result.Add(item);
            }
            ReadCursor(body); return result;
        }
        private static string HistoryLabel(JObject item) => FormatKst(item.Value<long>("AcceptedUtc")) + " KST  |  " +
            (item["Request"].Value<int>("TargetType") == 2 ? "전체" : "지정") + "  |  " +
            (item.Value<long?>("StoppedUtc").HasValue ? "중지" : item["Request"].Value<long>("DeliveryEndUtc") <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() ? "기간 종료" :
                item["Request"].Value<long?>("DeliveryStartUtc") > DateTimeOffset.UtcNow.ToUnixTimeSeconds() ? "예약" : "진행") +
            "\n" + item["Request"].Value<string>("RequestId") + "  " + item["Request"].Value<string>("Reason");
        internal static string Describe(long status, string json, string id, bool query)
        {
            if (status != 200) return "HTTP " + status + "\n" + (!string.IsNullOrEmpty(json) ? json :
                status == 0 ? "통신 실패 또는 제한시간 초과" : status == 401 ? "Azure Function 키와 우편 관리자 키를 확인해 주세요." : "서버가 요청을 거절했습니다.") +
                "\n기존 요청 ID는 유지됩니다. 이력을 조회해 확인하거나 같은 요청을 재전송하세요.";
            var body = ReadResponse(json, id);
            if (!query)
            {
                if (body["AcceptedUtc"]?.Type != JTokenType.Integer || body.Value<long?>("AcceptedUtc") <= 0 || body["AlreadyRegistered"]?.Type != JTokenType.Boolean)
                    throw new FormatException("Mail registration response missing.");
                return body.Value<long?>("StoppedUtc").HasValue ? "이미 중지된 발송입니다. 같은 요청을 다시 접수해도 재개하지 않습니다." :
                    "접수 성공 — 전달 완료는 아직 확인되지 않았습니다. 전체 발송은 기간 내 접속 확인 시, 지정 발송은 매분 처리합니다.";
            }
            var rows = body["Results"] as JArray ?? throw new FormatException("Mail result list missing.");
            string state = body.Value<long?>("StoppedUtc").HasValue ? "이 발송은 중지 상태입니다. 기존 우편·보상은 유지됩니다.\n" : "현재 페이지의 전달 결과입니다. 전체 유저의 완료·수령 통계는 아닙니다.\n";
            if (rows.Count == 0) return state + "아직 전달 결과가 없습니다. 이력이 비어 있다고 전체 전달 완료를 뜻하지 않습니다.";
            return state + string.Join("\n", rows.Select(row => row.Value<string>("PlayFabId") + ": " + (row.Value<int?>("Status") switch
            {
                1 => "전달 처리 중", 2 => "우편함 저장 완료 (보상 수령 여부는 별도)", 3 => "미전달 — " + row.Value<string>("Message"),
                4 => "쓰기 결과 확인 필요 — 새 우편으로 재발송하지 마세요.", 5 => "발송 기간 종료", 6 => "발송 중지로 미전달",
                _ => throw new FormatException("Unknown mail delivery status.")
            })));
        }
        private void UpdateControls()
        {
            if (_lookup == null) return;
            bool pending = !string.IsNullOrEmpty(_pending), busy = _request != null;
            _form.SetEnabled(!pending && !busy); _send.SetEnabled(!busy); _newMail.SetEnabled(!busy);
            _historyFirst.SetEnabled(!busy); _historyMore.SetEnabled(!busy && _historyNext != null); _historyView.SetEnabled(!busy); _lookup.SetEnabled(!busy);
            bool valid = ValidId(_lookup.value.Trim());
            _query.SetEnabled(!busy && valid); _resultMore.SetEnabled(!busy && valid && _resultNext != null);
            _stop.SetEnabled(!busy && valid && _knownId == _lookup.value.Trim() && !_knownStopped);
            _send.text = pending ? "같은 요청 재전송" : _draft.AllPlayers ? "전체 발송 등록" : "지정 발송 등록";
            try { _id.value = pending ? JObject.Parse(_pending).Value<string>("RequestId") : "발송 등록 시 생성"; }
            catch (JsonException) { _id.value = "복원되지 않은 이전 요청 — 이력에서 확인"; _send.SetEnabled(false); }
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
