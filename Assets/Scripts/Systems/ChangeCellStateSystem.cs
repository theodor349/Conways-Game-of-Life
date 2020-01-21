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
    public static float tickRate = 1f;
    private bool isRunning;
    private float timePassed = 0f;
    
    struct CellStateJob : IJobForEach<CellComponent, Translation>
    {
        [ReadOnly]
        public NativeArray<CellComponent> OtherCells;
        public int Width;
        public int Height;
        
        public void Execute(ref CellComponent cell, ref Translation pos)
        {
            int aliveNeighbours = 0;
            aliveNeighbours += GetStateOfCell(pos.Value.x, pos.Value.y + 1) ? 1 : 0;
            aliveNeighbours += GetStateOfCell(pos.Value.x + 1, pos.Value.y + 1) ? 1 : 0;
            aliveNeighbours += GetStateOfCell(pos.Value.x + 1, pos.Value.y) ? 1 : 0;
            aliveNeighbours += GetStateOfCell(pos.Value.x + 1 , pos.Value.y - 1) ? 1 : 0;

            aliveNeighbours += GetStateOfCell(pos.Value.x, pos.Value.y - 1) ? 1 : 0;
            aliveNeighbours += GetStateOfCell(pos.Value.x - 1, pos.Value.y - 1) ? 1 : 0;
            aliveNeighbours += GetStateOfCell(pos.Value.x - 1, pos.Value.y) ? 1 : 0;
            aliveNeighbours += GetStateOfCell(pos.Value.x - 1 , pos.Value.y + 1) ? 1 : 0;

            if (cell.IsAlive)
                cell.ChangeTo = aliveNeighbours == 2 || aliveNeighbours == 3;
            else
                cell.ChangeTo = aliveNeighbours == 3;
        }

        private bool GetStateOfCell(float x, float y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return false;
            var r = OtherCells[(int) (y + x * Height)].IsAlive;
            return r;
        }
    }
    
    struct GetOtherCells : IJob
    {
        public NativeArray<CellComponent> Cells;
        public NativeArray<Translation> Positions;
        public int N;
        public int Height;
        public void Execute()
        {
            var cc = new NativeArray<CellComponent>(N, Allocator.Temp);
        var pc = new NativeArray<Translation>(N, Allocator.Temp);
        N--;
        // Increment the size of the sub-arrays to be merged (sorted)
        for (int size = 1; size < N; size *= 2)
        {
            // Low Left index 
            int l1 = 0;
            // Index for inserting into temp arrays
            int k = 0;
            while (l1 + size < N)
            {
                // High Left index
                int h1 = l1 + size - 1;
                // Left High Index
                int l2 = h1 + 1;
                // Right High Index
                int h2 = l2 + size - 1;
                // In case Right High extends further than the array length
                if (h2 >= N)
                    h2 = N - 1;
                
                // Index for Left Array
                int i = l1;
                // Index for Right Array
                int j = l2;
                while (i <= h1 && j <= h2)
                {
                    // Sort
                    if (CompCells(Positions[i], Positions[j], Height))
                    {
                        cc[k] = Cells[i];
                        pc[k] = Positions[i];
                        i++;
                        k++;
                    }
                    else
                    {
                        cc[k] = Cells[j];
                        pc[k] = Positions[j];
                        j++;
                        k++;
                    }
                }
                
                // Insert the rest of the arrays
                while (i <= h1)
                {
                    cc[k] = Cells[i];
                    pc[k] = Positions[i];
                    i++;
                    k++;
                }
                while (j <= h2)
                {
                    cc[k] = Cells[j];
                    pc[k] = Positions[j];
                    j++;
                    k++;
                }
                
                // Increment the Left Low Array to the next one
                l1 = h2 + 1;
            }

            for (int i = l1; k < N; i++)
            {
                cc[k] = Cells[i];
                pc[k] = Positions[i];
                k++;
            }
            
            NativeArray<CellComponent>.Copy(cc, Cells);
            NativeArray<Translation>.Copy(pc, Positions);
        }
        cc.Dispose();
        pc.Dispose();
        }
        
        private bool CompCells(Translation a, Translation b, int height)
        {
            return (a.Value.y + a.Value.x * height) <= (a.Value.y + b.Value.x * height);
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

        // Get other cells
        var getOtherCellsJob = new GetOtherCells()
        {
            Cells = cells,
            Positions = pos,
            N = cells.Length,
            Height = SpawnSystem.WorldSize.y
        }.Schedule(inputDeps);
        
        // Actual job
        getOtherCellsJob.Complete();
//        SortCells(ref cells, ref pos, cells.Length, SpawnSystem.WorldSize.y);
        var cellStateJob = new CellStateJob()
        {
            OtherCells = cells,
            Width = SpawnSystem.WorldSize.x,
            Height = SpawnSystem.WorldSize.y
        }.Schedule(this, getOtherCellsJob);
        
        cellStateJob.Complete();
        cells.Dispose();
        pos.Dispose();
        return cellStateJob;
    }

    private void SortCells(ref NativeArray<CellComponent> cells, ref NativeArray<Translation> positions, int n, int height)
    {
        var cc = new NativeArray<CellComponent>(n, Allocator.Persistent);
        var pc = new NativeArray<Translation>(n, Allocator.Persistent);
        n--;
        // Increment the size of the sub-arrays to be merged (sorted)
        for (int size = 1; size < n; size *= 2)
        {
            // Low Left index 
            int l1 = 0;
            // Index for inserting into temp arrays
            int k = 0;
            while (l1 + size < n)
            {
                // High Left index
                int h1 = l1 + size - 1;
                // Left High Index
                int l2 = h1 + 1;
                // Right High Index
                int h2 = l2 + size - 1;
                // In case Right High extends further than the array length
                if (h2 >= n)
                    h2 = n - 1;
                
                // Index for Left Array
                int i = l1;
                // Index for Right Array
                int j = l2;
                while (i <= h1 && j <= h2)
                {
                    // Sort
                    if (CompCells(positions[i], positions[j], height))
                    {
                        cc[k] = cells[i];
                        pc[k] = positions[i];
                        i++;
                        k++;
                    }
                    else
                    {
                        cc[k] = cells[j];
                        pc[k] = positions[j];
                        j++;
                        k++;
                    }
                }
                
                // Insert the rest of the arrays
                while (i <= h1)
                {
                    cc[k] = cells[i];
                    pc[k] = positions[i];
                    i++;
                    k++;
                }
                while (j <= h2)
                {
                    cc[k] = cells[j];
                    pc[k] = positions[j];
                    j++;
                    k++;
                }
                
                // Increment the Left Low Array to the next one
                l1 = h2 + 1;
            }

            for (int i = l1; k < n; i++)
            {
                cc[k] = cells[i];
                pc[k] = positions[i];
                k++;
            }
            
            NativeArray<CellComponent>.Copy(cc, cells);
            NativeArray<Translation>.Copy(pc, positions);
        }
        cc.Dispose();
        pc.Dispose();
    }

    private bool CompCells(Translation a, Translation b, int height)
    {
        return (a.Value.y + a.Value.x * height) <= (a.Value.y + b.Value.x * height);
    }
}
