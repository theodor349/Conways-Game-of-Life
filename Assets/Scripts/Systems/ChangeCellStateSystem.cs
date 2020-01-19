using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class ChangeCellStateSystem : JobComponentSystem
{
    private bool isRunning;
    private float tickRate = 1f;
    private float timePassed = 0f;
    
    struct CellStateJob : IJobForEach<CellComponent, Translation>
    {
        public NativeArray<CellComponent> OtherCells;
        
        public void Execute(ref CellComponent cell, ref Translation transform)
        {
            cell.IsAlive = false;
        }
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        // Sim started
        if (Input.GetKeyDown(KeyCode.Space))
            isRunning = !isRunning;
        if (!isRunning) 
            return inputDeps;

        // Time 
        timePassed += UnityEngine.Time.deltaTime;
        if (timePassed >= tickRate)
            timePassed = 0f;
        else
            return inputDeps;

        // Get other cells
        var otherCells = GetEntityQuery(ComponentType.ReadOnly<CellComponent>(), ComponentType.ReadOnly<Translation>());
        var cells = otherCells.ToComponentDataArray<CellComponent>(Allocator.Persistent);
        var pos = otherCells.ToComponentDataArray<Translation>(Allocator.Persistent);

        NewNewSortCells(ref cells, ref pos, cells.Length, SpawnSystem.WorldSize.y);

        foreach (var p in pos)
        {
            Debug.Log(p.Value);
        }        
        // Actual job
        var job = new CellStateJob()
        {
            OtherCells = cells
        }.Schedule(this, inputDeps);

        job.Complete();
        cells.Dispose();
        pos.Dispose();
        return job;
    }

    private void NewNewSortCells(ref NativeArray<CellComponent> cells, ref NativeArray<Translation> positions, int n, int height)
    {
        var cc = new NativeArray<CellComponent>(n, Allocator.Persistent);
        var pc = new NativeArray<Translation>(n, Allocator.Persistent);
        NativeArray<CellComponent>.Copy(cells, cc, n);
        NativeArray<Translation>.Copy(positions, pc, n);

        n--;
        
        for (int size = 1; size < n; size *= 2)
        {
            int l1 = 0;
            int k = 0;
            while (l1 + size < n)
            {
                int h1 = l1 + size - 1;
                int l2 = h1 + 1;
                int h2 = l2 + size - 1;
                if (h2 >= n)
                    h2 = n - 1;

                int i = l1;
                int j = l2;

                while (i <= h1 && j <= h2)
                {
                    if (CompCells(pc[i], pc[j], height))
                    {
                        cells[k] = cc[i];
                        positions[k++] = pc[i++];
                    }
                    else
                    {
                        cells[k] = cc[j];
                        positions[k++] = pc[j++];
                    }
                }

                while (i <= h1)
                {
                        cells[k] = cc[i];
                        positions[k++] = pc[i++];
                }

                while (j <= h2)
                {
                    cells[k] = cc[j];
                    positions[k++] = pc[j++];                    
                }

                l1 = h2 + 1;
            }

            for (int i = l1; k < n; i++)
            {
                cells[k] = cc[i];
                positions[k++] = pc[i];
            }
        }
    }

    private bool CompCells(Translation a, Translation b, int height)
    {
        return (a.Value.y + a.Value.x * height) <= (a.Value.y + b.Value.x * height);
    }
}
