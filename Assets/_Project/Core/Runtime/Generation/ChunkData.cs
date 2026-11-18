using System;
using System.Collections.Generic;
using UnityEngine;

namespace TomCatRunner.Core.Generation
{
    /// <summary>
    /// 运行时 Chunk 数据 - 由 ProceduralLevelGenerator 产出，
    /// LevelStreamer 消费、ChunkPool 实例化
    /// </summary>
    [Serializable]
    public class ChunkData
    {
        /// <summary>Chunk 类型</summary>
        public TomCatRunner.Core.Data.ChunkType Type;

        /// <summary>起始 Z 坐标（米）</summary>
        public float StartZ;

        /// <summary>结束 Z 坐标（米）</summary>
        public float EndZ => StartZ + Length;

        /// <summary>长度（米）</summary>
        public float Length;

        /// <summary>所在车道掩码（bit0 = Lane0）</summary>
        public int LaneMask = 0b111;

        /// <summary>难度权重 0-1</summary>
        public float Difficulty01;

        /// <summary>变体索引（同一 Chunk 类型的不同预制体）</summary>
        public int VariationIndex;

        /// <summary>生成参数（供运行时组件读取）</summary>
        public Dictionary<string, float> Parameters = new Dictionary<string, float>();

        public ChunkData(TomCatRunner.Core.Data.ChunkType type, float startZ, float length)
        {
            Type = type;
            StartZ = startZ;
            Length = length;
        }

        public override string ToString() => $"{Type}[{StartZ:F0}~{EndZ:F0}]";
    }
}
