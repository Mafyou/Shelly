global using System.Collections.Frozen;
global using System.Collections.Immutable;
global using System.Collections.ObjectModel;
global using System.ComponentModel;
global using System.Diagnostics;
global using System.Globalization;
global using System.Runtime.InteropServices;
global using CommunityToolkit.Maui;
global using CommunityToolkit.Mvvm.ComponentModel;
global using CommunityToolkit.Mvvm.Input;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using Plugin.BLE;
global using Plugin.BLE.Abstractions;
global using Plugin.BLE.Abstractions.Contracts;
global using Plugin.BLE.Abstractions.EventArgs;
global using ShellyBLE.Core;
global using ShellyBLE.Core.BTHome;
global using ShellyBLE.Core.Buttons;
global using ShellyBLE.Core.Gatt;
global using ShellyBLE.Core.Shelly;
global using ShellyBLE.Models;
global using ShellyBLE.Services;
global using ShellyBLE.ViewModels;
global using ShellyBLE.Views;

#if ANDROID
global using AndroidPermission = Android.Manifest.Permission;
#endif
