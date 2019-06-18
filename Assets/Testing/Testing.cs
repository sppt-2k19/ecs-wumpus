using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Random = UnityEngine.Random;

public class Testing : MonoBehaviour
{
    public Material aMat;
    public Material mat;
    public Mesh mesh;

    private void Start()
    {
        var entityManager = World.Active.EntityManager;
        var ea = entityManager.CreateArchetype(
            typeof(LevelComponent),
            typeof(Translation),
            typeof(RenderMesh),
            typeof(LocalToWorld),
            typeof(MoveSpeed)
        );

//        var entity = entityManager.CreateEntity(typeof())


        var entArray = new NativeArray<Entity>(1000, Allocator.Temp);
        entityManager.CreateEntity(ea, entArray);

        for (var i = 0; i < entArray.Length; i++)
        {
            var ent = entArray[i];
            entityManager.SetComponentData(ent, new LevelComponent {level = 10 * i});
            entityManager.SetComponentData(ent, new MoveSpeed {speed = Random.Range(0.4f, 3f)});
            entityManager.SetComponentData(ent, new Translation {Value = new float3(Random.Range(-5f, 5f), 0, 0)});

            entityManager.SetSharedComponentData(ent, new RenderMesh
            {
                mesh = mesh,
                material = mat
            });
        }

        entArray.Dispose();
    }

    // Update is called once per frame
    private void Update()
    {
    }
}