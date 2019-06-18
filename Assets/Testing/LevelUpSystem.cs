using Unity.Entities;
using UnityEngine;

public class LevelUpSystem : ComponentSystem
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref LevelComponent levelComponent) => { levelComponent.level += 1f * Time.deltaTime; });
    }
}