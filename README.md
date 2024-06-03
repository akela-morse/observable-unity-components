[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

# Observable Unity Components

Watchable `MonoBehaviour` and `ScriptableObject` fields thanks to a Custom Source Generator and Roslyn Analyser specifically written for Unity.

## Description

This plugin allows you to get notified whenever the fields of your component are getting modified from anywhere within the engine, including from animation or within the editor. No refactoring is needed, and there is no need for verbose checks or interface implementation. Just make your `MonoBehaviour` or `ScriptableObject` class partial, add a `[Watch]` attribute to your fields, and then check for `HaveWatchedValuesChanged()` in your `Update()` method or anywhere else you like.

#### Sample code

```cs
using ObervableUnityComponents;
using UnityEngine;

public partial class MyObservableComponent : MonoBehaviour
{
    [Watch, SerializeField] private float myWatchedField;

    private void Update()
    {
        if (HaveWatchedValuesChanged())
        {
            Debug.Log("Who DARES changing my values?!");
        }
    }
}
```

## Getting Started

### Unity Version

* Unity 2020.2 minimum

### Installing

* Download the .unitypackage from the [Releases tab](https://github.com/akela-morse/observable-unity-components/releases/latest).
* Extract in your Unity project

> [!IMPORTANT]  
> This package *must* be installed in the **Assets** folder. Don't try to install this in your **Packages** folder; Roslyn Analysers will only work in Unity when using a [special asset label](https://docs.unity3d.com/2021.3/Documentation/Manual/roslyn-analyzers.html), and asset labels don't work in **Packages**.

### How to use

* Set your `MonoBehaviour` class or `ScriptableObject` class to partial
* Add a `[Watch]` attribute to any serialized field you want to watch
* Call `HaveWatchedValuesChanged()` to check whether or not the watched fields have been modified since the last check

## Known issues

* IntelliSense may throw an error when accessing generated identifiers such as `HaveWatchedValuesChanged()`. Closing VS and re-opening it might solve the issue.
* The first call to `HaveWatchedValuesChanged()` will always return `true` even if no values have changed. A temporary workaround is to add the following code before any calls to `HaveWatchedValuesChanged()` are made (but not after!)

```cs
this.observableGenerated_lastHash = this.observableGenerated_GetCurrentHash();
```

## F.A.Qs

> Why not implement generated properties like the `INotifyPropertyChanged` interface in MVVM applications?

This particular plugin is primarily written with Unity in mind. The goal here is to detect changes that happen from anywhere in the engine, including from an Animator or a Timeline component. Since Unity may change a component's fields using its internal serialization process, we cannot expect it to call our generated properties. The `HaveWatchedValuesChanged()` method aligns more with a regular Unity workflow.

## Author

Akela Morse
[@AkelaMorse](https://x.com/AkelaMorse)

## Version History

* 0.2
	* Implemented support for derived classes with watchable fields
* 0.1
    * Initial Release

## License

This project is licensed under the Apache 2.0 License - see the LICENSE file for details
