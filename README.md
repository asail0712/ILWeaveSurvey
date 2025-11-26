# XPlan IL Weaving System

本專案提供一套基於 **Mono.Cecil** 的 Unity IL Weaving 系統，用於在編譯期自動注入 UI、MVVM、I18N 與通知註冊邏輯，減少樣板程式碼並提升執行效率。

## 功能總覽

### Log 自動添加
- 使用 `[LogAspect]` 標記的方法會自動添加Log。
- 會提示方法開始與結束以及執行花費的時間。

### MVVM 按鈕自動綁定
- 依照命名規則自動將 ViewModel 方法（例如 `OnLoginClick`）綁定到 View 上對應的 `loginBtn`。
- 也可透過 Attribute 指定對應的 Button 欄位名稱。
- 降低手動註冊 `onClick.AddListener` 的重複程式碼。

### I18N 多語系 UI 掛載
- 由 Weaver 在 View 的 `Awake()` 自動插入 I18N 註冊呼叫，例如：
  ```csharp
  I18NWeaverRuntime.Register(this);
  ```
- UI 元件會根據目前語系自動更新文字與資源。

### NotifyHandler 自動註冊
- 對方法加上 Attribute：
  ```csharp
  [NotifyHandler]
  private void ShowError(LoginErrorMsg msg) { ... }
  ```
- 編譯期自動插入對應的 `RegisterNotify<LoginErrorMsg>` 註冊程式碼。
- 免去在建構子或初始化流程中手動訂閱通知。

### 擴充式 Weaving 架構
- 支援 `IMethodAspectWeaver`、`IFieldAspectWeaver`、`ITypeAspectWeaver` 等擴充點。
- Editor 會自動掃描實作並註冊，不需修改核心程式即可新增自訂 Weaver。
- 方便依專案需求增加更多 AOP 功能。

## Editor 工具

- `XPlanTools/Weaver/Toggle Enabled`：啟用或停用整體 IL Weaving。
- `XPlanTools/Weaver/Run Weaver Now`：手動立即執行 Weaving，方便測試與除錯。
