# 发布指南（含 Termux）

本文说明如何把这个项目发布到 GitHub，并让 GitHub Actions 在云端构建出 6 个平台的产物、自动发 Release。

---

## 0. 先搞清楚一件事

**Termux 本身不能构建这个项目。**

原因不是配置问题，而是硬限制：

| 需求 | Termux（Android/ARM64）现状 |
|---|---|
| .NET 10 SDK | 官方不支持 Android 作为构建平台 |
| Avalonia 桌面窗口 | 依赖 X11/桌面图形栈，Android 上无法运行桌面版 |
| Native AOT 交叉编译 | 需要目标平台的链接器；无法在手机上产出 Windows `.exe` |

所以正确的做法是：**Termux 只负责"把代码推上去 + 打个 tag"，编译交给 GitHub 的云端机器**。

这个项目已经配好了流水线：

- `.github/workflows/build-and-release.yml`：打 `v*` tag 时构建 win-x64 / win-arm64 / linux-x64 / linux-arm64 / osx-x64 / osx-arm64，并自动创建 Release
- `.github/workflows/ci.yml`：PR 和非 main 分支的构建检查

---

## 1. 准备（Termux）

```bash
pkg update && pkg upgrade -y
pkg install git openssh gh unzip -y
```

> 若 `gh` 装不上（不同源可能缺包），就用 SSH 认证，见下面「认证」的第二条。

把项目解压到 home：

```bash
cd ~
unzip /sdcard/Download/BongoCatNotice.zip
cd BongoCatNotice
```

> 项目约 61MB（`Assets/` 占 55MB），建议在 WiFi 下操作。

## 2. 认证

**方式一：gh 登录（推荐）**

```bash
gh auth login
```
选择 `GitHub.com` → `HTTPS` → `Login with a web browser`，复制一次性代码到浏览器授权即可。

**方式二：SSH key**

```bash
ssh-keygen -t ed25519 -C "termux@bongocatnotice"
cat ~/.ssh/id_ed25519.pub
```
把输出整行贴到 GitHub → Settings → SSH and GPG keys → New SSH key。

## 3. 初始化并推送

```bash
cd ~/BongoCatNotice

git init
git branch -M main
git add .
git commit -m "feat: BongoCatNotice v0.1.0.3"
```

用 gh 一步建仓库并推送（把 `你的用户名` 换掉）：

```bash
gh repo create 你的用户名/BongoCatNotice --public --source=. --remote=origin --push
```

不用 gh 的话手动来：

```bash
git remote add origin https://github.com/你的用户名/BongoCatNotice.git
git push -u origin main
```

> ⚠️ 如果在网页上先建了仓库，**不要**勾选「添加 README / .gitignore / LICENSE」，否则远程仓库非空，`push` 会报 `rejected (fetch first)`。真遇到了就：
> `git pull --rebase origin main` 然后再 push。

## 4. 触发云端构建并发布

打 tag 并推送（tag 必须以 `v` 开头，否则不触发）：

```bash
git tag v0.1.0.3
git push origin v0.1.0.3
```

随后 GitHub Actions 会自动：
1. 在 6 个 runner 上分别 `dotnet publish`
2. 非 Windows 产物 `strip` + UPX 压缩
3. 打包成 6 个 zip，创建 Release 并附上更新说明（自动从 `changelog.md` 顶部提取）

## 5. 手机上看进度与下载

```bash
# 查看运行记录
gh run list

# 实时跟踪当前运行
gh run watch

# 查看发布结果
gh release view v0.1.0.3

# 直接下载产物到手机
gh release download v0.1.0.3 -p "*win-x64*" -D ~/Download
```

Windows 产物是 `BongoCatNotice.win-x64.zip`，解压后运行 `BongoCatNotice.exe`。

---

## 常见问题

**Q：首次 push 很慢 / 中途失败？**
61MB 的仓库在移动网络下容易断。建议 WiFi；仍失败可分两次推（先推 `Assets/` 之外的代码，再推资源），或改用电脑推一次。

**Q：Actions 一直不跑？**
- tag 是不是以 `v` 开头
- 仓库 Settings → Actions → General 里是否允许 Actions 运行
- 仓库是不是 fork（fork 默认不跑 workflow）

**Q：Windows 构建失败提示找不到 MSVC？**
`windows-latest` runner 自带。若自己改成了 `-p:PublishAot=true` 之外的配置导致找不到链接器，可加 `-p:PublishAot=false` 走普通自包含发布。

**Q：能手动构建、不发布吗？**
可以。GitHub 网页 → Actions → 对应 workflow → Run workflow（需要 workflow 里配了 `workflow_dispatch`；当前两个 workflow 只配了 push 触发，可以自己加一行）。

**Q：只改文档也要发版吗？**
不需要，push 到 main 即可，Release 只在推 tag 时创建。

---

## 附：在电脑上发布（更省事）

如果手边有电脑，流程更简单：

```bash
git init
git branch -M main
git add .
git commit -m "feat: BongoCatNotice v0.1.0.3"
git remote add origin https://github.com/你的用户名/BongoCatNotice.git
git push -u origin main

git tag v0.1.0.3
git push origin v0.1.0.3
```

之后手机上只需要看结果。
