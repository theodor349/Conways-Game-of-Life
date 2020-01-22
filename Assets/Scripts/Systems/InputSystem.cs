using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Properties;
using Unity.Transforms;
using UnityEngine;

public class InputSystem : JobComponentSystem
{
    private Camera cam;

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        
        cam = Camera.main;
    }

    struct ChangeCellState : IJobForEach<CellComponent, Translation>
    {
        public float3 Pos;
        
        public void Execute(ref CellComponent cell, ref Translation pos)
        {
            if ((int) (Pos.x + 0.5f) == (int) pos.Value.x && (int) (Pos.z + 0.5f) == (int) pos.Value.z)
                cell.ChangeTo = !cell.IsAlive;
        }
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (!Input.GetMouseButtonDown(0))
            return inputDeps;
        
        return new ChangeCellState()
        {
            Pos = cam.ScreenToWorldPoint(Input.mousePosition)
        }.Schedule(this, inputDeps);
    }
}
