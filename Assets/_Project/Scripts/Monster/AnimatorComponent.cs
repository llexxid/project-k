using Scripts.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimatorComponent<TAction> where TAction : Enum
{
    Animator _am;
    Dictionary<TAction, int> _hashCode;

    public AnimatorComponent(Animator am, Dictionary<TAction, int> _dic)
    {
        _am = am;
        _hashCode = _dic;
    }

    public bool TrySetBool(TAction action, bool value)
    {
        bool IsExistKey;
        IsExistKey = _hashCode.TryGetValue(action, out int hashValue);
        if (!IsExistKey)
        {
            return false;
        }
        _am.SetBool(hashValue, value);
        return true;
    }

    public bool TrySetTrigger(TAction action)
    {
        bool IsExistKey;
        IsExistKey = _hashCode.TryGetValue(action, out int hashValue);
        if (!IsExistKey)
        {
            return false;
        }
        _am.SetTrigger(hashValue);
        return true;
    }

}
