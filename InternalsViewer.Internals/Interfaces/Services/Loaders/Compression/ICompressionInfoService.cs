﻿using System.Threading.Tasks;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;

public interface ICompressionInfoService
{
    Task<CompressionInfo?> GetCompressionInfo(Page page);
}