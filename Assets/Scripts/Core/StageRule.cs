namespace Scripts.Core
{
    public static class StageRule 
    {
        private const ulong WaveMask = 0x000000000000FFFF; //웨이브 검출용 마스크
        private const ulong StageNumberMask = 0x000000007FFF0000; //스테이지 번호 검출용 마스크
        private const ulong StageBaseMask = 0xFFFFFFFFFFFF0000; //스테이지 베이스 검출용 마스크
        private const int WaveBitSize = 16; //웨이브 할당 비트
        private const ulong BossWaveNumber = 11;
        
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
        /// 그 위의 15비트(0x7FFF0000)는 스테이지 번호 영역이다.
        /// <br/>따라서 0x7FFF0000으로 스테이지 영역만 남긴 뒤 
        /// 하위 16비트만큼 오른쪽으로 이동해서 실제 번호로 변환한다.
        /// </remarks>
        public static int GetStageNumber(eStage stage)
        {
            ulong value = (ulong)stage;
            return (int)((value & StageNumberMask) >> WaveBitSize);
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
            ulong value = (ulong)stage;
            return (int)(value & WaveMask);
        }

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
            ulong stageBase = (ulong)currentStage & StageBaseMask;
            return (eStage)(stageBase + BossWaveNumber);
        }

        /// <summary>
        /// 현재 스테이지 값을 기준으로 다음 웨이브 또는 다음 스테이지를 계산한다.
        /// </summary>
        /// <param name="currentStage">현재 진행 중인 스테이지/웨이브 값.</param>
        /// <param name="result">계산된 다음 스테이지/웨이브 값.</param>
        /// <returns>
        /// 일반 웨이브 이동이면 <see cref="eStageResult.WaveChanged"/>,
        /// <br/>보스 웨이브 진입이면 <see cref="eStageResult.BossWaveEntered"/>,
        /// <br/>보스 웨이브 이후 다음 스테이지로 넘어가면 <see cref="eStageResult.StageChanged"/>를 반환한다.
        /// </returns>
        /// <remarks>
        /// 현재 규칙에서는 11웨이브를 보스 웨이브로 사용한다.
        /// <br/>10웨이브에서 다음으로 이동하면 보스 웨이브 진입,
        /// 11웨이브에서 다음으로 이동하면 다음 스테이지 1웨이브로 전환된다.
        /// </remarks>
        public static eStageResult GetNextWave(eStage currentStage, out eStage result)
        {
            ulong wave = ((ulong)currentStage & WaveMask);
            if (wave == BossWaveNumber)
            {
                ulong stageAdder = 0x0000000000010001; // 첫번째 스테이지로 가기위해 +1
                //기존 스테이지의 베이스 스테이지로 이동 후 다음 1스테이지로 이동
                result = (eStage)(((ulong)currentStage & StageBaseMask) + stageAdder );
                return eStageResult.StageChanged;
            }
            result = (eStage)((ulong)++currentStage);
            return ((ulong)result & WaveMask) == BossWaveNumber ? eStageResult.BossWaveEntered : eStageResult.WaveChanged;
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
            ulong wave = ((ulong)currentStage & WaveMask);

            if (wave <= 1)
            {
                result = currentStage;
                return eStageResult.None;
            }

            result = (eStage)((ulong)--currentStage);
            return eStageResult.WaveChanged;
        }
    }
}
