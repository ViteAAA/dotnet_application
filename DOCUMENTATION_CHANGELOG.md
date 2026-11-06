# Техническая документация изменений ветки mariettka

В данном документе детально зафиксированы все изменения, внесенные в исходный код C# решения `WebApplication1.sln`, причины возникавших ошибок и руководство по конфигурации шаблонов.

---

## 1. Анализ выявленных ошибок и причин их возникновения

### Ошибка 1: Построчный разрозненный вывод товаров в кастомном чеке
- **Файл:** `PrintingSlips/Services/PrintDocumentBuilder.cs` и `PrintDocumentBuilder.Lines.cs`
- **Проблема:** Метод `BuildTemplateLines` для ключа `"block.items"` генерировал последовательность строк для каждого вложенного поля каждого товара. При генерации текстовых строк (`BuildTemplateTextLines`) и ESC/POS байтов (`BuildTemplateEscPos`) формировались строки:
  `Наименование: <Название>`
  `Кол-во: <Количество>`
  `Цена: <Цена>`
  `Сумма: <Сумма>`
  Это нарушало требования к кассовому пречеку, где позиции должны выводиться компактной таблицей с колонками.
- **Причина:** Отсутствие отдельной логики верстки табличной части для пользовательских шаблонов.
- **Исправление:** Добавлен параметр `ItemDisplayMode` ("Table" / "Kitchen" / "List"). Реализована табличная верстка с заголовками колонок (`Наименование`, `К-во`, `Цена`, `Сумма`), разделителями и выравниванием для GDI (WindowsQueuePrinter), ESC/POS и QuestPDF.

---

### Ошибка 2: Невозможность удаления полей в блоке вывода заказов и в главном списке
- **Файлы:** `ApplicationView/ReceiptSettingsWindow.xaml`, `ReceiptSettingsWindow.xaml.cs`
- **Проблема:** Пользователь не мог удалить поле или блок товаров из списка чека.
- **Причина:**
  1. В `ReceiptSettingsWindow.xaml` не было индивидуальных кнопок удаления для строк основных полей.
  2. Кнопка «❌ Удалить поле» в правой панели ориентировалась на `LstReceiptFields.SelectedIndex`. При клике внутрь любого элемента ввода (TextBox, ComboBox, Border блока `block.items`) элемент `ListBoxItem` терял фокус, и `SelectedIndex` становился равен `-1`. Повторные нажатия на кнопку «Удалить поле» ничего не делали.
  3. Вложенные подполя внутри `block.items` удалялись методом `BtnRemoveSubField_Click`, который вызывал `Remove(subField)` без предварительной проверки вхождения в коллекцию родителя.
- **Исправление:**
  1. В шаблон элемента каждого поля `ReceiptField` добавлена прямая кнопка «❌» (`BtnRemoveFieldItem_Click`), удаляющая элемент напрямую из коллекции `ReceiptFields` через его `DataContext`.
  2. Подполя блока `block.items` снабжены выделенными кнопками удаления с надежным поиском родителя (`Contains(subField)`).
  3. Общая кнопка удаления на панели доработана для безопасной проверки как выбранного объекта (`SelectedItem`), так и индекса (`SelectedIndex`).

---

### Ошибка 3: Отсутствие пунктирных строк и настроек межстрочного интервала
- **Файлы:** `PrintingSlips/Models/ReceiptField.cs`, `PrintingSlips/Models/PrintTextLine.cs`, `PrintDocumentBuilder.Lines.cs`, `WindowsQueuePrinter.cs`, `PdfSlipExporter.cs`
- **Проблема:** Невозможно было вставить пунктирную линию или настроить отступы между строками.
- **Причина:** В моделях `ReceiptField` и `PrintTextLine` не были предусмотрены свойства стиля разделителя и вертикального отступа.
- **Исправление:**
  1. В `ReceiptField` добавлены свойства:
     - `bool IsSeparator` (флаг разделителя);
     - `string SeparatorStyle` ("Dashed", "Solid", "Dotted", "Stars", "Double");
     - `int LineSpacing` (дополнительный отступ снизу в пунктах);
     - `bool IsRow` (вывод в две колонки: метка слева, значение справа);
     - `string ItemDisplayMode` (режим отображения товаров).
  2. В `PrintTextLine` добавлены `SeparatorStyle` и `SpacingAfter`.
  3. В `WindowsQueuePrinter.cs` реализована отрисовка пунктирных линий с использованием `pen.DashStyle = DashStyle.Dash` / `DashStyle.Dot` и учет `line.SpacingAfter`.
  4. В `MainWooCommerceOptions` добавлены специальные ключи:
     - `separator.dashed`
     - `separator.solid`
     - `separator.stars`
     - `separator.double`
     - `space.empty`
  5. В окно `ReceiptSettingsWindow.xaml` добавлены кнопки «➕ Пунктир» и «␣ Пустой отступ».

---

### Ошибка 4: Сбой `NullReferenceException` в `PrintTestPageAsync`
- **Файл:** `PrintingSlips/Services/PrintingService.cs` (строка 76)
- **Проблема:** Вызов `_documentBuilder.BuildFormLines(PrintFormType.TestPage, null!, width)` передавал `null` вместо объекта `Slip`. При обращении к веткам по умолчанию возникало исключение `NullReferenceException`.
- **Исправление:** Сигнатура `BuildFormLines` обновлена на `Slip? slip`, добавлены проверки на `null` и возврат `BuildTestPageLines`.

---

### Ошибка 5: Сбой `UnauthorizedAccessException` при сохранении PDF превью
- **Файлы:** `PrintingSlips/Services/PrintingService.cs`, `PdfSlipExporter.cs`
- **Проблема:** Файлы `check_{id}.pdf` и `test_page_{...}.pdf` сохранялись в `AppContext.BaseDirectory`. Если приложение запущено из защищенной директории (например, `C:\Program Files\...`), Windows выбрасывал `UnauthorizedAccessException`, что прерывало процесс печати.
- **Исправление:** Все временные PDF сохраняются в безопасную пользовательскую директорию `%AppData%\PrintingSlips\PdfExports\`. Добавлены `try-catch` блоки, гарантирующие, что сбой генерации PDF не нарушит отправку задания на термопринтер.

---

### Ошибка 6: Обрезка подвала чека в очереди Windows
- **Файл:** `PrintingSlips/Services/WindowsQueuePrinter.cs`
- **Проблема:** Предварительный расчет высоты `MeasureHeight` не учитывал аппаратные поля рулонного принтера (`HardMarginX`, непечатаемые границы драйвера). Длинные чеки обрезались ножом до вывода итоговых строк и подвала.
- **Исправление:** В `MeasureHeight` добавлен коэффициент запаса 1.15 и буфер 80 единиц (1/100 дюйма) для гарантированного прогона ленты под нож отрезчика.

---

## 2. Сводка измененных файлов проекта

| Файл | Описание изменений |
|---|---|
| `PrintingSlips/Models/ReceiptField.cs` | Добавлены `IsSeparator`, `SeparatorStyle`, `LineSpacing`, `IsRow`, `ItemDisplayMode`, хелперы `CheckIsSeparator()`, `GetEffectiveSeparatorStyle()`, `CheckIsSpacer()` |
| `PrintingSlips/Models/PrintTextLine.cs` | Добавлены свойства `SeparatorStyle` и `SpacingAfter` |
| `PrintingSlips/Models/Slip.cs` | Добавлено свойство `Subtotal` в класс `SlipItem` |
| `PrintingSlips/Services/PrintDocumentBuilder.Lines.cs` | Поддержка `Slip? slip`, отступов `spacingAfter`, разделителей со стилями, вывод позиций `block.items` в виде таблицы и кухонного чека |
| `PrintingSlips/Services/PrintDocumentBuilder.cs` | Обновлен `BuildTemplateEscPos` для поддержки пунктира, звездочек, отступов и табличной части товаров в ESC/POS |
| `PrintingSlips/Services/WindowsQueuePrinter.cs` | Отрисовка пунктирных (`DashStyle.Dash`), точечных, двойных линий и звездочек, отступов между строками и защита от обрезки чека |
| `PrintingSlips/Services/PdfSlipExporter.cs` | Обновлен `ExportTemplatePdf`: верстка таблицы товаров как в накладной QuestPDF, рендеринг разделителей, 2 колонок и отступов |
| `PrintingSlips/Services/PrintingService.cs` | Безопасная тестовая печать без null-exception, безопасное сохранение PDF в `%AppData%\PrintingSlips\PdfExports\` |
| `ApplicationView/Services/ProcessingData.cs` | Дефолтные шаблоны `GetDefaultInvoiceTemplate()` и `GetDefaultKitchenTemplate()` дополнены разделителями, 2-колоночными строками и режимом таблицы |
| `ApplicationView/ReceiptSettingsWindow.xaml` | Добавлены кнопки быстрого удаления полей «❌», кнопки «➕ Пунктир», «␣ Пустой отступ», выбор режима товаров `ItemDisplayMode`, настройка `LineSpacing` и `IsRow`, кнопки сброса шаблона |
| `ApplicationView/ReceiptSettingsWindow.xaml.cs` | Расширена `MainWooCommerceOptions`, реализованы обработчики прямого удаления, вставки разделителей и сброса шаблонов |
| `WebAPI/WebAPI.csproj` | Изменен TargetFramework на `net9.0-windows` для полной совместимости со сборкой решения |
| `README.md` | Полное руководство пользователя и разработчика |

---

## 3. Инструкция по проверке и тестированию

1. **Сборка проекта:**
   ```bash
   dotnet build WebApplication1.sln -c Debug
   ```
2. **Проверка конструктора чеков:**
   - Запустите `ApplicationView.exe`.
   - Нажмите кнопку **«⚙️ Настроить поля»**.
   - Нажмите **«🔄 Накладная 58мм»** — структура чека заполнится полями с разделителями и таблицей товаров.
   - Нажмите **«➕ Пунктир»** — в чек добавится пунктирная строка.
   - Нажмите **«❌»** на любом поле — поле мгновенно удалится.
   - В блоке товаров удалите любой параметр или нажмите **«+ Добавить параметр товара»**.
   - Нажмите **«Сохранить»**.
3. **Проверка печати:**
   - Выберите принтер в списке главного окна.
   - Нажмите кнопку **«Тест печати»**. Принтер напечатает тестовый чек с проверкой русского/английского шрифта, размеров и отрезчика.
   - Выберите заказ из таблицы и нажмите **«Печать чека вручную»** — чек распечатается в соответствии с настроенным шаблоном.
   - Проверьте PDF копию чека в папке `%AppData%\PrintingSlips\PdfExports\`.
