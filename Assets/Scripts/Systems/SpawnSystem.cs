using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public class SpawnSystem : ComponentSystem
{
    public static int2 WorldSize;
    
    private EntityArchetype cellArchetype;
    private Material deadMaterial;
    private Mesh planeMesh;

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        cellArchetype = World.DefaultGameObjectInjectionWorld.EntityManager.CreateArchetype(
            typeof(LocalToWorld),
            typeof(RenderMesh),
            typeof(Translation),
            typeof(CellComponent),
            typeof(Scale)
        );
        
        // Material
        deadMaterial = new Material(Shader.Find("Standard"));
        deadMaterial.color = Color.black;
        // Mesh
        var p = ObjectFactory.CreatePrimitive(PrimitiveType.Plane);
        planeMesh = p.GetComponent<MeshFilter>().mesh;
        Object.Destroy(p);
    }

    protected override void OnUpdate()
    {
        Entities.ForEach((Entity entity, ref WorldSpawnSize worldSize) =>
        {
            ChangeCellStateSystem.tickRate = worldSize.TickRate;
            
            WorldSize = new int2(worldSize.Width, worldSize.Height);
            
            for (int x = 0; x < worldSize.Width; x++)
            {
                for (int y = 0; y < worldSize.Height; y++)
                {
                    bool alive = false;

                    if (x == 4 && y == 4)
                        alive = true;
                    else if (x == 3 && y == 4)
                        alive = true;
                    else if (x == 5 && y == 4)
                        alive = true;
//                    else if (x == 4 && y == 3)
//                        alive = true;
//                    else if (x == 4 && y == 5)
//                        alive = true;

                    var c = PostUpdateCommands.CreateEntity(cellArchetype);
                    PostUpdateCommands.SetComponent(c, new LocalToWorld());
                    PostUpdateCommands.SetSharedComponent(c, new RenderMesh(){material = deadMaterial, mesh = planeMesh});
                    PostUpdateCommands.SetComponent(c, new Translation(){Value = new float3(x, 0, y)});
                    PostUpdateCommands.SetComponent(c, new CellComponent(){ChangeTo = alive});
                    PostUpdateCommands.SetComponent(c, new Scale(){Value = 0.1f});
                }
            }
            
            PostUpdateCommands.DestroyEntity(entity);
        });
    }

}
