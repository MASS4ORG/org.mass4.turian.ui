global using System.Globalization;
global using System.Numerics;
global using System.Reflection;
global using System.Text;
global using System.Text.Json;
global using MASS4.Bricks;
global using Guinevere;
global using Ktx2;
global using Microsoft.Extensions.Logging.Abstractions;
global using NSubstitute;
global using OpenAssetPackage;
global using Silk.NET.Vulkan;
global using SkiaSharp;
global using Turian.Editor.Core;
global using Turian.Editor.UI;
global using Turian.Engine.Core;
global using Turian.Engine.UI;
global using Xunit;
global using GKey = Guinevere.KeyboardKey;
global using GMouseButton = Guinevere.MouseButton;
global using GuiColor = Guinevere.Color;
global using Key = Silk.NET.Input.Key;
global using SilkMouseButton = Silk.NET.Input.MouseButton;

// Test classes run in parallel; those sharing process-wide state opt into the serial SerialTests collection.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.Collections)]
