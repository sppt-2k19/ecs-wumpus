using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class AgentSystem : ComponentSystem
{

    protected override void OnUpdate()
    {
        Entities.ForEach((ref AgentComp agentComp, ref Translation translation) =>
        {
            agentComp.spent += Time.deltaTime;
            if (Vector3.Distance(translation.Value.xyz, agentComp.target) < Mathf.Epsilon)
                translation.Value = new float3(agentComp.target);
            else
                translation.Value = Vector3.Slerp(translation.Value, agentComp.target, agentComp.spent / agentComp.dur);
        });
    }
}