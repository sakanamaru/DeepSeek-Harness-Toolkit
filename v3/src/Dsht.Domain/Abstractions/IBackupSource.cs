using System;
using System.Collections.Generic;
using Dsht.Domain.Model;

namespace Dsht.Domain.Abstractions
{
    /// <summary>备份来源（平台实现负责读盘）。有效性判定仍由领域层的 BackupPackage 负责。</summary>
    public interface IBackupSource
    {
        /// <summary>备份根（v2.x 语义：状态目录/backup）。</summary>
        string BackupsRoot { get; }

        /// <summary>列出备份根下 dsh-data-* 直接子目录，按名字升序（旧 → 新），与 v2.x 排序一致。</summary>
        List<BackupEntry> ListRaw();

        /// <summary>目录总字节数（递归累加文件长度，跳过读不到的项）。</summary>
        long DirSize(string path);

        /// <summary>目录最后写入时间；取不到返回 null（对应 v2.x 的 "(unknown)"）。</summary>
        DateTime? LastWrite(string path);
    }
}