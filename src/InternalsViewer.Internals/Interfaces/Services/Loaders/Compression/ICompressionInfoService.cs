﻿using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;

public interface ICompressionInfoService
{
    CompressionInfo? GetCompressionInfo(AllocationUnitPage page);
}