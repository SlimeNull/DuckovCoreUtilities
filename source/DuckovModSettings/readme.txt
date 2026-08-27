Duckov Mod Settings 为《逃离鸭科夫》添加统一的游戏内模组设置页面。它会自动发现其他模组的 MonoBehaviour 设置，无需模组作者引用本项目、注册设置或依赖专用 API。

[h1]玩家功能[/h1]

[list]
[*]在主菜单和游戏内设置面板中添加“模组”标签页。
[*]使用模组名称和图标展示已发现的模组。
[*]按真实对象结构显示可折叠的多级设置组。
[*]支持搜索设置名称、提示文本和当前值。
[*]支持布尔开关、数字输入、范围滑块、字符串、字符、枚举、按键及颜色编辑。
[*]颜色选择器支持 RGBA 数值和 HEX 文本。
[*]提供“恢复默认”按钮，可一次重置当前模组的所有选项。
[*]自动保存设置，并在模组下次加载时恢复。
[*]扫描只在“模组设置”页面打开时执行，并按模组分帧处理；加载期间会显示进度。
[/list]

设置保存在：

[code]Application.persistentDataPath/DuckovModSettings/settings.json[/code]

保存时会使用临时文件和 .bak 备份。

[h1]订阅[/h1]

订阅此模组及其必需依赖，然后重启游戏。无需手动修改其他模组。

只有符合下述发现规则的模组设置会出现在页面中；没有公开可配置成员的模组不会显示。

[h1]模组作者接入[/h1]

Duckov Mod Settings 会检查每个已加载的 Duckov.Modding.ModBehaviour 根对象，并扫描：

[list]
[*]与模组根组件挂在同一个 GameObject 上；
[*]与模组根组件定义在同一个程序集中的 MonoBehaviour。
[/list]

以下实例成员会成为设置：

[list]
[*]公开字段；
[*]公开且可读写的属性；
[*]标记 [b]SerializeField[/b] 或 [b]SerializeReference[/b] 的非公开字段。
[/list]

静态、常量、只读、索引器以及标记 [b]HideInInspector[/b] 的成员会被忽略。标记 [b]Serializable[/b] 的嵌套类和结构体会显示为真正的多级设置组；数组、集合、委托和 UnityEngine.Object 引用目前不会作为可编辑设置显示。

[h2]支持的值类型[/h2]

[list]
[*]bool
[*]string、char
[*]所有整数类型
[*]float、double、decimal
[*]枚举
[*]UnityEngine.KeyCode
[*]UnityEngine.InputSystem.Key
[*]UnityEngine.Color、Color32
[*]上述值类型的可空形式
[/list]

[h2]支持的 Unity 特性[/h2]

[list]
[*][b]InspectorName[/b]：设置显示名称。
[*][b]Tooltip[/b]：鼠标悬停提示。
[*][b]Header[/b]：插入分区标题。
[*][b]Range[/b]：使用滑块并约束数值范围。
[*][b]TextArea[/b]：使用多行字符串编辑器。
[*][b]HideInInspector[/b]：从设置页面隐藏成员。
[/list]

[h2]最小示例[/h2]

[code][Serializable]
private sealed class NetworkOptions
{
    [InspectorName("端口")]
    [Tooltip("重新启动服务后生效")]
    [Range(1024, 65535)]
    public int Port = 37622;

    [InspectorName("状态颜色")]
    public Color StatusColor = Color.green;
}

[SerializeField, InspectorName("网络")]
private NetworkOptions network = new NetworkOptions();[/code]

[h1]应用与通知[/h1]

用户编辑值后，成员会立即更新，并在存在无参 OnValidate() 时调用它。用户离开或关闭设置页面后，每个发生过编辑的组件所在 GameObject 会收到一次可选消息：

[code]private void DuckovModSettingsUpdated()
{
    // 提交需要在设置页面关闭后统一应用的变更。
}[/code]

消息使用 SendMessageOptions.DontRequireReceiver 发送，因此不实现该方法也不会报错。

[h1]设置文本本地化[/h1]

[b]InspectorName[/b]、[b]Header[/b]、[b]Tooltip[/b] 以及枚举项的 [b]InspectorName[/b] 可以使用程序集资源：

[list]
[*][b]@文本键[/b]：从设置所属程序集中找到的第一个 ResourceManager 读取该键。
[*][b]@资源类型/文本键[/b]：按简单名称或完全限定名称查找资源类型，再使用该类型构建或取得 ResourceManager。
[/list]

示例：

[code][InspectorName("@SettingsText/ListenPort")]
public int Port = 37620;[/code]

游戏切换语言时，Duckov Mod Settings 会同步资源类型的 Culture 或 ResourceCulture 并刷新已打开的页面。找不到资源类型或键时，会保留原始 @... 文本，便于定位配置问题。

[h1]反馈与源码[/h1]

遇到问题时，请在评论区留言，或到 [url=https://github.com/SlimeNull/DuckovMods]GitHub 仓库[/url]提交 Issue。

此模组有使用 GPT 5.6 Sol 辅助开发及生成文档.

[h1]贡献翻译[/h1]

欢迎为此模组补充其他语言：

[olist]
[*]复制项目中的 [b]Localization/SettingsText.resx[/b]，也可以从已有翻译文件开始。
[*]将副本命名为 [b]SettingsText.<语言标识>.resx[/b]，例如 [b]ja[/b]、[b]ko[/b]、[b]ru[/b]。
[*]只翻译每个资源项的值，保留所有键名以及 [b]{0}[/b]、[b]{1}[/b] 等占位符。
[*]完成后向 [url=https://github.com/SlimeNull/DuckovMods]GitHub 仓库[/url]提交 Pull Request。
[/olist]
