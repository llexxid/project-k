
using System;

namespace Scripts.Core.Manager
{
	public static class StageParser
	{
		const double stageRatioMultiplier = 0.4;
		const double waveRatioMultiplier = 0.04;
		const int templateStageCount = 2;

		public const long WaveMask = 0x000000000000FFFF; //웨이브 검출용 마스크
		public const long StageNumberMask = 0x000000000FFF0000; //스테이지 번호 검출용 마스크
		public const long ContentTypeMask = 0x00000000F0000000; //스테이지 타입 검출용 마스크
		public const ulong StageBaseMask = 0xFFFFFFFFFFFF0000; //스테이지 베이스 검출용 마스크
		public const int WaveBitSize = 16; //웨이브 할당 비트
		public const long BossWaveNumber = 11;

		/// <summary>
		/// eStage 값에서 스테이지 번호만 추출한다.
		/// </summary>
		/// <remarks>
		/// eStage는 하나의 정수값 안에 스테이지 번호와 웨이브 번호를 함께 저장한다.
		/// 
		/// <br/>예:
		/// Stage1   = 0x200010000
		/// <br/>Stage1_1 = 0x200010001
		/// <br/>Stage1_2 = 0x200010002
		/// <br/>Stage2   = 0x200020000
		/// 
		/// <br/>여기서 하위 16비트(0x0000FFFF)는 웨이브 번호이고,
		/// 그 위의 12비트(0xFFF0000)는 스테이지 번호 영역이다.
		/// <br/>따라서 0xFFF0000으로 스테이지 영역만 남긴 뒤 
		/// 하위 16비트만큼 오른쪽으로 이동해서 실제 번호로 변환한다.
		/// </remarks>
		public static int GetStageNumber(eStage stage)
		{
			return (int)((long)stage & StageNumberMask) >> WaveBitSize;
		}

		/// <summary>
		/// eStage 값에서 웨이브 번호만 추출한다.
		/// </summary>
		/// <remarks>
		/// eStage의 하위 16비트는 웨이브 번호로 사용된다.
		/// 
		/// <br/>예:
		/// Stage1   = 0x200010000 -> wave 0
		/// <br/>Stage1_1 = 0x200010001 -> wave 1
		/// <br/>Stage1_10 = 0x20001000A -> wave 10
		/// 
		/// <br/>따라서 0x0000FFFF 마스크를 적용하면 스테이지/분류 영역은 제거되고 웨이브 번호만 남는다.
		/// </remarks>
		public static int GetWaveNumber(eStage stage)
		{
			return (int)((long)stage & WaveMask);
		}

		/// <summary>
		/// eStage 값에서 스테이지 타입을 추출한다
		/// </summary>
		/// <remarks>
		/// eStage의 8번째 비트는 스테이지 타입의 종류로 설정한다. (0x0000000000000000 ~ 0x0000000070000000)
		/// 
		/// <br/>종류 :
		/// <br/>메인 스테이지 : 0x0000000000000000
		/// <br/>골드 던전 : 0x0000000010000000
		/// <br/>루비 던전 : 0x0000000020000000
		/// 
		/// <br/>따라서 0x00000000F0000000 마스크를 적용하면 스테이지 타입만 반환된다
		/// </remarks>
		public static eStageType GetStageType(eStage stage)
		{
			switch ((long)stage & ContentTypeMask)
			{
				case 0x0000000000000000:
					return eStageType.Main;
				case 0x0000000010000000:
					return eStageType.GoldDungeon;
				case 0x0000000020000000:
					return eStageType.RubyDungeon;
				default:
					throw new ArgumentNullException();
			}
		}
		/// <summary>
		/// 특정 스테이지 그룹의 몬스터, 몬스터 SFX, 몬스터 VFX 로딩 상태를 함께 추적한다.
		/// <br/>* 몬스터 프리팹 로딩은 MonsterSpawner가 UniTask로 진행하며,
		/// LoadManager는 반환된 Task를 기다려 스테이지 리소스 준비 완료 시점만 맞춘다.
		/// </summary>
		//현재의 스테이지를 기반으로 ResourceGroupID를 얻어냄.
		public static ulong GetResourceGroupId(eStage curStage)
		{
			return ((ulong)curStage & 0xFFFFFFFFFFFF0000);
		}
		
		public static eStage GetFixedStageKey(eStage stage)
		{

			long value = (long)stage;
			long stageMask = 0x00000000FFFF0000;
			
			// ContentType 0은 메인 스테이지다.
			// 던전은 각자의 데이터를 직접 조회한다.
			if ((value & ContentTypeMask) != 0)
				return stage;
			
			int stageNumber = GetStageNumber(stage);
			//현재 스테이지 리소스가 2개이므로 1,3,5 등 홀수 스테이지는 1스테이지 리소스, 2,4,6 등 짝수 스테이지는 2스테이지 리소스 사용
			int templateStageNumber =
				((stageNumber - 1) % templateStageCount) + 1;
			
			// 콘텐츠 타입과 웨이브는 유지하고 스테이지 번호만 교체한다.
			value &= ~StageNumberMask;
			value |= (long)templateStageNumber << WaveBitSize;

			return (eStage)value;
		}

		public static double GetRatio(eStage stage)
		{
			double ret = 1.0;
			long stageNumber = GetStageNumber(stage);
			long waveNumber = GetWaveNumber(stage);

			if (stageNumber > 2)
			{
				double pointNumber = 0;
				pointNumber = waveNumber * waveRatioMultiplier;
				// - ���������� 0.4�辿 ������.
				//3���������� 1.2��, wave�� 0.04��.
				//4���������� 1.6��
				//5���������� 2��  
				ret = (stageNumber * stageRatioMultiplier) + pointNumber;
			}
			return ret;
		}
		
		public static bool IsBossWave(eStage stage) => GetWaveNumber(stage) == (int)BossWaveNumber;

		#region 미사용 유틸리티
		/// <summary>
		/// 현재 스테이지와 같은 스테이지 그룹의 보스 웨이브 eStage 값을 반환한다.
		/// </summary>
		/// <remarks>
		/// 우리 게임에서는 각 스테이지 그룹의 11웨이브를 보스 스테이지로 사용한다.
		/// 
		/// <br/>eStage는 하위 16비트에 웨이브 번호를 저장하므로
		/// 우선 StageBaseMask로 웨이브 번호를 제거해 스테이지 그룹 기준값만 남긴다.
		/// <br/>그 뒤 보스 웨이브 번호인 11을 더해 보스 eStage를 만든다.
		/// 
		/// 예:
		/// Stage1_3  = 0x200010003
		/// <br/>base      = 0x200010000
		/// <br/>boss      = 0x200010000 + 11 = Stage1_11
		/// </remarks>
		public static eStage GetBossStage(eStage currentStage)
		{
			ulong stageBase = (ulong)currentStage & StageParser.StageBaseMask;
			return (eStage)(stageBase + StageParser.BossWaveNumber);
		}
		
		/// <summary>
		/// 현재 스테이지 값을 기준으로 이전 웨이브를 계산한다.
		/// </summary>
		/// <param name="currentStage">현재 진행 중인 스테이지/웨이브 값.</param>
		/// <param name="result">계산된 이전 스테이지/웨이브 값. 이전 웨이브가 없으면 현재 값을 그대로 반환한다.</param>
		/// <returns>
		/// 이전 웨이브로 이동할 수 있으면 <see cref="eStageResult.WaveChanged"/>,
		/// 현재 웨이브가 1 이하라 이동할 수 없으면 <see cref="eStageResult.None"/>을 반환한다.
		/// </returns>
		/// <remarks>
		/// 현재 규칙에서는 1웨이브보다 이전으로 이동하지 않는다.
		/// 보스 실패 후 10웨이브 복귀처럼 특수한 이동은 이 메서드 대신
		/// 보스 상태 해제 후 명시적으로 복귀 웨이브를 지정하는 방식이 더 적합하다.
		/// </remarks>
		public static eStageResult GetPreviousWave(eStage currentStage, out eStage result)
		{
			ulong wave = ((ulong)currentStage & StageParser.WaveMask);

			if (wave <= 1)
			{
				result = currentStage;
				return eStageResult.None;
			}

			result = (eStage)((ulong)--currentStage);
			return eStageResult.WaveChanged;
		}

		#endregion
	}
}