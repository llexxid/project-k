using Scripts.Core;
using Scripts.Core.SO;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class DropManager : MonoBehaviour
{
	public static DropManager Instance;
	[SerializeField]
	DropTableMetaSO _DropTableMetaSO;
	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			Init();
			DontDestroyOnLoad(gameObject);
			return;
		}
		Destroy(gameObject);
	}

	private void Init()
	{
		_DropTableMetaSO.Init();
	}

	public DropInfo GetDropInfo(eDropTable tableId)
	{
		DropInfo ret;
		_DropTableMetaSO.TryGetDropInfo(tableId, out ret);
		return ret;
	}
}
