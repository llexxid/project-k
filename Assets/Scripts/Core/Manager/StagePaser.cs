
namespace Scripts.Core.Manager
{
	public static class StageParser
	{
		const double stageRatioMultiplier = 0.4;
		const double waveRatioMultiplier = 0.04;
		public static long GetStageNumber(eStage stage)
		{
			return ((long)stage & 0x00000000FFFF0000) >> 16;
		}

		public static long GetWaveNumber(eStage stage)
		{
			return ((long)stage & 0x000000000000FFFF) >> 16;
		}

		public static eStage GetFixedStageKey(eStage stage)
		{
			long stageNumber = GetStageNumber(stage);
			long waveNumber = GetWaveNumber(stage);

			ulong stageMask = 0x00000000FFFF0000;
			ulong stageUniuqId = 0x0000000200000000;

			//stage가 짝수인지 홀수인지 검사
			ulong stageKey;
			//1,3,5,7...stage는 bandit들 
			if ((stageNumber) % 2 == 1)
			{
				stageKey = ((ulong)stage & ~stageMask) | 0x0000000000010000; // stage만 1로 바꾸기
			}
			else
			{
				stageKey = ((ulong)stage & ~stageMask) | 0x0000000000020000; //stage만 2로 바꾸기
			}
			stageKey = stageKey | stageUniuqId;
			return (eStage)stageKey;
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
				// - 스테이지당 0.4배씩 강해짐.
				//3스테이지는 1.2배, wave당 0.04배.
				//4스테이지는 1.6배
				//5스테이지는 2배  
				ret = (double)(stageNumber * stageRatioMultiplier) + pointNumber;
			}
			return ret;
		}
	}
}