using System.Collections.Generic;
using Core.Stage;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Core.SO;

public interface IStageDefinitionProvider
{
    bool TryGet(eStage id, out StageDefinition definition);
}
