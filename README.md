
# MetaSpriteEx

MetaSpriteEx是一个Unity插件，它可以快速地导入[Aseprite][aseprite]的.ase文件到Unity中，生成Animation Clips，Animation Contoller还可以选择生成GameObject。它还具备元数据(metadata)的支持，它可以控制生成的sprite锚点位置，在动画剪辑中设置Tramform和BoxCollider2D(可以添加更多)的参数关键帧，还有Event功能

这个仓库派生自[WeAthFoLD/MetaSprite][WeAthFoLD]，部分说明来自原文
初次尝试，若发现问题，欢迎反馈

# OverView

* 不需要外界的aseprite程序就可以使用
* ~~Blazing fast~~ 由于多个部件打包，它看上去没有原来的快
* 高效的图集打包
* 简洁的工作流，简单地配置完后，点击~~就送~~
* 元数据支持——来自图层名字
  * 忽略图层/图层组
  * 控制Transform,BoxCollider2D,Sprite中的pivot
  * 将动画剪辑标记为循环
  * 把跨越层次的图层归纳起来(生成GameObject时有用)
* 一个图层组，意味着一个独立的图集，一个GameObject，可以用于复杂的分块需求
* 可以自行对元数据进行拓展
* 可以指定生成的GameObject所属的SpriteLayer，并且按照原来的顺序排序

# Differences

与[WeAthFoLD/MetaSprite][WeAthFoLD]相比，这个插件
* 移除掉了SubImage，用更直观的**图层组**层次来代替这种功能来应对更复杂的需求(如act像素游戏))
* 将需要指定目标名字的meta语法，更替为把**同图层组**图层作为目标
* 可以按照图层组层次, 生成同样层次GameObject的预制体
* 生成的GameObject中，所有SpriteRender组件中的排序与ase中排序相同，且可以选择排序组
* 添加了新的语法 "GroupName=>someTarget"，用于归纳**多个穿插层次**的图层组

# Start

Check out [releases](https://github.com/Teemodisi/MetaSprite/releases) for unitypackage downloads. You can copy the `Assets/Plugins` folder of the repo into your unity project's asset folder for same effect.

See [wiki](https://github.com/Teemodisi/MetaSprite/wiki) for explanation of importing, import settings, meta layers and other importer features.

# Credits

* [WeAthFoLD](https://github.com/WeAthFoLD)'s original Aseprite importer
* [tommo](https://github.com/tommo)'s Aseprite importer in gii engine
* [talecrafter](https://github.com/talecrafter)'s [AnimationImporter](https://github.com/talecrafter/AnimationImporter), where the code of this project is initially based from

# Donation

Here is WeAthFoLD's Donation:
If this plugin helped you in your project or you want to support the developement, consider buying me a cup of coffee (°∀°)ﾉ

<a href="https://www.patreon.com/bePatron?u=2955382">
<img src="https://c5.patreon.com/external/logo/become_a_patron_button.png"/>
</a>

[aseprite]: https://aseprite.org
[WeAthFoLD]: (https://github.com/WeAthFoLD/MetaSprite)