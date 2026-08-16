using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Server.DTO;
using Scripts.Wallets;
using KingdomIdle.MageTower;
using KingdomIdle.KingdomArmy;
using PlayFab.CloudScriptModels;
using Newtonsoft.Json;

namespace KingdomIdle.Gacha
{
	using ItemCode = Scripts.Server.DTO.ItemCode;
	using SkillCode = Scripts.Server.DTO.SkillCode;

	/// <summary>
	/// 가챠 매니저.
	/// - ClassFragment / ArcaneKnowledge 통화 → PlayFab CloudScript 서버 가챠
	/// - 그 외(Gold 등 테스트용) → 클라이언트 가중 롤
	/// - 낙관적 재화 차감 후 서버 오류 시 자동 롤백
	/// - In-flight 락으로 중복 요청 차단
	/// - OnPullStateChanged 이벤트로 UI(버튼 활성/비활성) 동기화
	/// </summary>
	public class GachaManager : MonoBehaviour
	{
		public static GachaManager Instance { get; private set; }

		[SerializeField]
		private List<GachaTableSO> gachaTables;

		public IReadOnlyList<GachaTableSO> GetAllTables() => gachaTables;

		// ── In-flight 락 ───────────────────────────────────────────────
		private bool _isPulling;
		public bool IsPulling => _isPulling;

		/// <summary>풀 시작/완료 시 호출. UI 버튼 활성 여부 동기화에 사용.</summary>
		public event Action<bool> OnPullStateChanged;

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			DontDestroyOnLoad(gameObject);
		}

		private void OnDestroy()
		{
			if (Instance == this) Instance = null;
		}

		// ── 사전 검사 ──────────────────────────────────────────────────

		public bool CanPull(GachaTableSO table)
		{
			if (table == null || !table.isImplemented) return false;
			EconomyBridge.TryGetAmount(table.costCurrency, out long cur);
			return cur >= table.costAmount;
		}

		public int GetTotalCost(GachaTableSO table, int count) =>
			table != null ? table.costAmount * count : 0;

		public bool CanPullMulti(GachaTableSO table, int count)
		{
			if (table == null || !table.isImplemented || count <= 0) return false;
			EconomyBridge.TryGetAmount(table.costCurrency, out long cur);
			return cur >= (long)table.costAmount * count;
		}

		/// <summary>
		/// AncientCoin(장비 가챠) / ArcaneKnowledge(스킬 가챠) 통화로 뽑는 테이블은
		/// 서버 CloudScript 로 라우팅된다(서버에 `OnGachaEquipmentClassFragment` /
		/// `OnGachaSkillArcaneKnowledge` CloudScript 함수가 존재).
		/// 장비 가챠는 서버에서 10% 확률로 전직 파편(ClassFragment) 이 섞여 떨어진다.
		/// 그 외(Gold 등 테스트용)는 클라이언트 가중 롤.
		/// </summary>
		private static bool IsServerBacked(GachaTableSO table) =>
			table != null &&
			(table.costCurrency == eCurrency.AncientCoin ||
			 table.costCurrency == eCurrency.ArcaneKnowledge);

		// ── 퍼블릭 진입점 ─────────────────────────────────────────────

		/// <summary>
		/// 가챠 요청(비동기). 서버 응답을 받아 콜백으로 결과 전달.
		/// 서버 에러 시 차감된 재화를 롤백한다.
		/// 이미 진행 중인 요청이 있으면 중복 실행을 방지한다.
		/// </summary>
		public void TryPull(GachaTableSO table, int count,
							Action<List<GachaRewardEntry>> onSuccess,
							Action<string> onError)
		{
			if (_isPulling)
			{
				onError?.Invoke("이미 뽑기가 진행 중입니다.");
				return;
			}

			if (table == null)
			{
				onError?.Invoke("뽑기 테이블이 유효하지 않습니다.");
				return;
			}

			if (!table.isImplemented)
			{
				onError?.Invoke("미구현 기능입니다.");
				return;
			}

			if (count <= 0)
			{
				onError?.Invoke("뽑기 횟수가 잘못되었습니다.");
				return;
			}

			int totalCost = table.costAmount * count;
			EconomyBridge.TryGetAmount(table.costCurrency, out long cur);
			if (cur < totalCost)
			{
				onError?.Invoke("재화가 부족합니다.");
				return;
			}

			// 서버 가챠는 세션이 확립되어야 가능
			if (IsServerBacked(table) && !IsNetworkReady())
			{
				onError?.Invoke("네트워크 세션이 준비되지 않았습니다.");
				return;
			}

			// 락 설정 + 낙관적 차감
			SetPulling(true);
			EconomyBridge.Add(table.costCurrency, -totalCost);

			if (IsServerBacked(table))
			{
				RequestServerPull(table, count, totalCost, onSuccess, onError);
			}
			else
			{
				var results = RollClient(table, count);
				if (results == null || results.Count == 0)
				{
					FailWithRefund(table, totalCost, "뽑기에 실패했습니다.", onError);
					return;
				}
				CompleteSuccess(results, onSuccess);
			}
		}

		// ── 내부 상태 ──────────────────────────────────────────────────

		private void SetPulling(bool v)
		{
			if (_isPulling == v) return;
			_isPulling = v;
			OnPullStateChanged?.Invoke(v);
		}

		private void CompleteSuccess(List<GachaRewardEntry> results,
									 Action<List<GachaRewardEntry>> onSuccess)
		{
			SetPulling(false);
			onSuccess?.Invoke(results);
		}

		private void FailWithRefund(GachaTableSO table, int refundAmount,
									string message, Action<string> onError)
		{
			if (refundAmount > 0 && table != null)
				EconomyBridge.Add(table.costCurrency, refundAmount);
			SetPulling(false);
			Debug.LogWarning($"[GachaManager] 실패: {message}");
			onError?.Invoke(message);
		}

		private static bool IsNetworkReady()
		{
			var net = NetworkManager.Instance;
			if (net == null) return false;
			string sid = net.GetSessionID();
			return !string.IsNullOrEmpty(sid);
		}

		// ── 서버 가챠 ──────────────────────────────────────────────────

		private void RequestServerPull(GachaTableSO table, int count, int refundAmount,
									   Action<List<GachaRewardEntry>> onSuccess,
									   Action<string> onError)
		{
			var net = NetworkManager.Instance;
			if (net == null)
			{
				FailWithRefund(table, refundAmount, "네트워크가 초기화되지 않았습니다.", onError);
				return;
			}

			// 장비 가챠(AncientCoin) — 서버 CloudScript `OnGachaEquipmentClassFragment`
			// 스킬 가챠(ArcaneKnowledge) — 서버 CloudScript `OnGachaSkillArcaneKnowledge`
			if (table.nameEng == "Equipment")
			{
				net.OnGachaEquipmentClick(count,
					result => HandleEquipmentResponse(result, onSuccess, onError, table, refundAmount),
					error => HandleServerError(error, onError, table, refundAmount));
			}
			else if (table.nameEng == "MageTowerSkill")
			{
				net.OnGachaSkillClick(count,
					result => HandleSkillResponse(result, onSuccess, onError, table, refundAmount),
					error => HandleServerError(error, onError, table, refundAmount));
			}
			else
			{
				FailWithRefund(table, refundAmount, "지원하지 않는 재화입니다.", onError);
			}
		}

		private void HandleEquipmentResponse(ExecuteFunctionResult result,
											 Action<List<GachaRewardEntry>> onSuccess,
											 Action<string> onError,
											 GachaTableSO table, int refundAmount)
		{
			if (result == null || result.FunctionResult == null)
			{
				FailWithRefund(table, refundAmount, "서버 응답이 비어있습니다.", onError);
				return;
			}

			OnGachaEquipmentClassFragmentResponseDTO dto;
			try
			{
				string json = JsonConvert.SerializeObject(result.FunctionResult);
				dto = JsonConvert.DeserializeObject<OnGachaEquipmentClassFragmentResponseDTO>(json);
			}
			catch (Exception ex)
			{
				FailWithRefund(table, refundAmount, $"응답 파싱 실패: {ex.Message}", onError);
				return;
			}

			if (dto?.GachaList == null || dto.GachaList.Count == 0)
			{
				FailWithRefund(table, refundAmount, "서버에서 보상을 받지 못했습니다.", onError);
				return;
			}

			var equipDB = KingdomArmyManager.Instance?.EquipDB;
			if (equipDB == null)
			{
				FailWithRefund(table, refundAmount, "장비 데이터베이스가 없습니다.", onError);
				return;
			}

			// 서버 응답을 엔트리로 변환 + 즉시 지급.
			// 뽑기 자체는 서버 CloudScript 가 수행하고 결과만 내려준다.
			// 클라이언트는 그 결과를 해석/표시하고 통합 전직 파편 지급만 반영한다.
			//
			// 서버는 동일 ItemCode 를 한 건으로 묶어 내려보내고 개수를
			// ItemCode 하위 16비트(= GetItemAmount) 에 Count 로 인코딩한다.
			// 장비/파편 모두 동일한 규칙을 사용한다.
			var results = new List<GachaRewardEntry>(dto.GachaList.Count);
			foreach (var itemCode in dto.GachaList)
			{
				int code = (int)itemCode.GetItemCode();
				EquipmentData data = equipDB.GetEquipmentByCode(code);

				if (data != null)
				{
					// 장비 드롭 — 서버가 동일 장비 N 개를 한 엔트리로 묶어 내려줄 수 있다.
					int equipCount = (int)itemCode.GetItemAmount();
					if (equipCount <= 0) equipCount = 1;

					var equipEntry = new GachaRewardEntry
					{
						rewardType = eGachaRewardType.Equipment,
						equipmentData = data,
						nameKor = data.equipmentName,
						icon = data.icon,
						amount = equipCount,
					};

					// N 개 지급 — EquipmentInstance 는 인스턴스별로 생성해야 하므로 반복 호출.
					for (int i = 0; i < equipCount; i++)
						DistributeEquipmentReward(equipEntry);

					results.Add(equipEntry);
				}
				else
				{
					// 장비 DB 에 없는 ItemCode 는 통합 전직 파편(ClassFragment) 드롭으로 간주.
					// 서버가 장비 가챠에 섞어 내려보내는 10% 확률 파편이다.
					//
					// 통합 전직 파편 체제:
					//   - 직업별 파편이 아니라 단일 파편 재화 (eCurrency.ClassFragment) 로 누적.
					//   - ItemCode 의 jobFlag(= GetItemJobCode) 는 무시해도 무방하나,
					//     서버가 직업 구분을 계속 내려보낸다면 보관만 하고 지급은 통합 풀로 진행.
					//   - GetItemAmount() 가 파편 개수.
					int fragmentAmount = (int)itemCode.GetItemAmount();
					if (fragmentAmount <= 0) fragmentAmount = 1;

					var fragmentEntry = new GachaRewardEntry
					{
						rewardType = eGachaRewardType.Currency,
						currency = eCurrency.ClassFragment,
						amount = fragmentAmount,
						nameKor = "전직 파편",
					};

					var armyMgr = KingdomArmyManager.Instance;
					if (armyMgr != null)
					{
						armyMgr.AddFragments(fragmentAmount);
					}
					else
					{
						// KingdomArmyManager 가 없어도 Wallet 에 직접 반영해 손실을 방지.
						EconomyBridge.Add(eCurrency.ClassFragment, fragmentAmount);
						Debug.LogWarning("[GachaManager] KingdomArmyManager 미초기화 — Wallet 에 직접 지급.");
					}

					results.Add(fragmentEntry);
				}
			}

			// 서버 응답의 전직파편 총량으로 클라이언트 동기화
			if (dto.ClassFragmentCnt >= 0)
			{
				EconomyBridge.TryGetAmount(eCurrency.ClassFragment, out long curFragment);
				long diff = dto.ClassFragmentCnt - curFragment;
				if (diff != 0)
					EconomyBridge.Add(eCurrency.ClassFragment, (int)diff);
			}

			if (results.Count == 0)
			{
				SetPulling(false);
				Debug.LogWarning("[GachaManager] 서버는 보상을 내려줬으나 클라이언트에서 해석 가능한 항목이 없습니다.");
				onError?.Invoke("보상 데이터가 올바르지 않습니다. 관리자에게 문의해주세요.");
				return;
			}

			CompleteSuccess(results, onSuccess);
		}

		private void HandleSkillResponse(ExecuteFunctionResult result,
										 Action<List<GachaRewardEntry>> onSuccess,
										 Action<string> onError,
										 GachaTableSO table, int refundAmount)
		{
			if (result == null || result.FunctionResult == null)
			{
				FailWithRefund(table, refundAmount, "서버 응답이 비어있습니다.", onError);
				return;
			}

			OnGachaSkillArcaneKnowledgeResponseDTO dto;
			try
			{
				string json = JsonConvert.SerializeObject(result.FunctionResult);
				dto = JsonConvert.DeserializeObject<OnGachaSkillArcaneKnowledgeResponseDTO>(json);
			}
			catch (Exception ex)
			{
				FailWithRefund(table, refundAmount, $"응답 파싱 실패: {ex.Message}", onError);
				return;
			}

			/* 비전지식만 뽑힐 수도 있습니다.
            if (dto?.GachaList == null || dto.GachaList.Count == 0)
            {
                FailWithRefund(table, refundAmount, "서버에서 보상을 받지 못했습니다.", onError);
                return;
            }*/

			var mtMgr = MageTowerManager.Instance;
			if (mtMgr == null)
			{
				FailWithRefund(table, refundAmount, "마탑 매니저가 없습니다.", onError);
				return;
			}

			// 서버는 동일 SkillCode 를 한 건으로 묶어 내려보내고 개수를
			// SkillCode 하위 16비트(= GetSkillAmount) 에 Count 로 인코딩한다.
			var results = new List<GachaRewardEntry>(dto.GachaList.Count);

			foreach (var skillCode in dto.GachaList)
			{
				int skillId = (int)skillCode.GetSkillId();
				var so = mtMgr.GetSkillById(skillId);
				if (so == null)
				{
					Debug.LogWarning($"[GachaManager] skillId {skillId} 에 해당하는 MageTowerSkillSO 없음 — 건너뜀");
					continue;
				}

				int skillCount = (int)skillCode.GetSkillAmount();
				if (skillCount <= 0) skillCount = 1;

				var entry = new GachaRewardEntry
				{
					rewardType = eGachaRewardType.Skill,
					skillId = skillId,
					amount = skillCount,
					nameKor = so.nameKor,
					icon = so.icon,
				};

				// 스킬 파편 N 개 지급.
				mtMgr.AddFragments(skillId, skillCount);
				results.Add(entry);
			}

			// 서버 응답의 비전지식 총량으로 클라이언트 동기화.
			// 서버 응답 SkillCode 리스트에는 스킬만 포함되며, 비전지식 드롭은
			// ArcaneKnowledgeCnt(누적 총량) 과 현재 지갑 잔액의 차이로만 판단 가능하다.
			// 차감(서버) 후 지갑 대비 서버 총량이 양수 = 드롭. 뽑기 팝업에 보상 엔트리로 추가.
			if (dto.ArcaneKnowledgeCnt >= 0)
			{
				EconomyBridge.TryGetAmount(eCurrency.ArcaneKnowledge, out long curAK);
				UserManager.Instance.GainAracneKnowledge(dto.ArcaneKnowledgeCnt);

				results.Add(new GachaRewardEntry
				{
					rewardType = eGachaRewardType.Currency,
					currency = eCurrency.ArcaneKnowledge,
					amount = (int)dto.ArcaneKnowledgeCnt,
					nameKor = "비전지식",
				});

			}

			if (results.Count == 0)
			{
				SetPulling(false);
				Debug.LogWarning("[GachaManager] 서버는 스킬 보상을 내려줬으나 클라이언트에서 해석 가능한 스킬이 없습니다.");
				onError?.Invoke("스킬 데이터가 올바르지 않습니다. 관리자에게 문의해주세요.");
				return;
			}

			CompleteSuccess(results, onSuccess);
		}

		private void HandleServerError(PlayFab.PlayFabError error,
									   Action<string> onError,
									   GachaTableSO table, int refundAmount)
		{
			string msg = error != null ? error.ErrorMessage : "알 수 없는 서버 오류";
			FailWithRefund(table, refundAmount, $"서버 오류: {msg}", onError);
		}

		// ── 클라이언트 롤 (테스트/비서버 통화용) ──────────────────────

		private List<GachaRewardEntry> RollClient(GachaTableSO table, int count)
		{
			var results = new List<GachaRewardEntry>(count);
			for (int i = 0; i < count; i++)
			{
				var reward = table.Roll();
				if (reward != null)
				{
					DistributeReward(reward);
					results.Add(reward);
				}
			}
			return results;
		}

		private void DistributeReward(GachaRewardEntry reward)
		{
			switch (reward.rewardType)
			{
				case eGachaRewardType.Currency:
					EconomyBridge.Add(reward.currency, reward.amount);
					break;

				case eGachaRewardType.Skill:
					var mtMgr = MageTowerManager.Instance;
					if (mtMgr != null)
						mtMgr.AddFragments(reward.skillId, reward.amount);
					break;

				case eGachaRewardType.Equipment:
					DistributeEquipmentReward(reward);
					break;

				case eGachaRewardType.DivineCard:
					var divineMgr = KingdomIdle.Divine.DivineSkillManager.Instance;
					if (divineMgr != null)
					{
						// 최초 획득 = 보유 등록(+미해금이면 자동 해금), 중복 = 레벨업 재료 적립.
						divineMgr.Acquire(reward.divineCardId);
					}
					else
					{
						// 이 시점에는 이미 재화가 차감된 뒤라 보상 유실이 확정된다 —
						// 조용히 넘기면 안 되므로 에러로 남긴다 (bootstrap 씬 매니저 배치 확인 필요).
						Debug.LogError($"[GachaManager] DivineSkillManager 미초기화 — " +
									   $"신 스킬 카드(id {reward.divineCardId}) 보상이 지급되지 못했습니다.");
					}
					break;
			}
		}

		/// <summary>
		/// 장비 보상을 직업이 호환되는 플레이어(없으면 첫 번째)의 인벤토리에 추가한다.
		/// </summary>
		private void DistributeEquipmentReward(GachaRewardEntry reward)
		{
			if (reward.equipmentData == null)
			{
				Debug.LogWarning("[GachaManager] Equipment 보상이지만 equipmentData가 null입니다.");
				return;
			}

			var armyMgr = KingdomArmyManager.Instance;
			if (armyMgr == null)
			{
				Debug.LogWarning("[GachaManager] KingdomArmyManager가 없어 장비를 지급할 수 없습니다.");
				return;
			}

			var players = armyMgr.GetPlayers();
			if (players == null || players.Count == 0)
			{
				Debug.LogWarning("[GachaManager] 플레이어가 없어 장비를 지급할 수 없습니다.");
				return;
			}

			Player targetPlayer = players[0];
			for (int i = 0; i < players.Count; i++)
			{
				var p = players[i];
				if (p?.PlayerEquipmentManager == null) continue;

				var changeJob = p.GetComponent<ChangeJob>();
				if (changeJob == null) continue;

				if (reward.equipmentData.IsAllowedForJob(p.playerStatus?.JobName ?? ""))
				{
					targetPlayer = p;
					break;
				}
			}

			var instance = new EquipmentInstance(reward.equipmentData);
			EquipmentManager.Instance.GetEquipment(instance,GetEffect.None);
			

			Debug.Log($"[GachaManager] 장비 지급: {reward.equipmentData.equipmentName} ({reward.equipmentData.rarity}) → {targetPlayer.name}");
		}
	}
}
