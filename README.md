# Plus API Toolset

Набор инструментов для модов Baldi's Basics Plus на BepInEx.

## Сборка

Нужен .NET SDK с поддержкой `.slnx` (например, установленный здесь .NET 10).
Для сборки требуется установленная игра и BepInEx: DLL берутся из игры,
скачивать их через NuGet не нужно.

1. Скопируйте `GamePath.example.props` в `GamePath.user.props` в корне репозитория.
2. Укажите в `GamePath` свою папку игры. Локальная настройка исключена из Git.
3. Запустите из корня:

```powershell
dotnet build .\PlusAPIToolset.slnx
dotnet build .\PlusAPIToolset.slnx -c Release
```

`build` автоматически восстанавливает служебные файлы NuGet в `obj`.
В качестве альтернативы локальному файлу можно передать путь при сборке:

```powershell
dotnet build .\PlusAPIToolset.slnx '-p:GamePath=D:\Program Files (x86)\Steam\steamapps\common\Baldi''s Basics Plus'
```

Без локальной настройки используется стандартная папка Steam в Program Files (x86).
Если обязательной DLL нет, сборка сообщает её путь.

Готовый плагин: `PlusAPIToolset/bin/Release/netstandard2.1/PlusAPIToolset.dll`.
Игровые DLL и BepInEx не копируются в результат сборки.

## Пример

`Examples/TestSoda.cs` сохранён как пример и по умолчанию исключён из DLL,
чтобы библиотека не регистрировала тестовый предмет у пользователя.
Для экспериментальной сборки с примером:

```powershell
dotnet build .\PlusAPIToolset.slnx -p:IncludeExamples=true
```

## Проверка в игре

Компиляция не проверяет поведение Unity и совместимость Harmony-патчей.
После изменений проверьте регистрацию соды, спрайты и звук, переход между
сценами, генерацию этажей и магазин в отдельном игровом запуске.
Известные проблемы и результаты ревью находятся в `REVIEW.md`.
