using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class MoverSystem : ComponentSystem
{
    protected override void OnUpdate()
    {
        Entities.ForEach((ref Translation translation, ref MoveSpeed moveSpeed) =>
        {
            translation.Value.y += moveSpeed.speed * Time.deltaTime;
            if (translation.Value.y > 5f) moveSpeed.speed = -math.abs(moveSpeed.speed);
            if (translation.Value.y < -5f) moveSpeed.speed = +math.abs(moveSpeed.speed);
        });
    }
}