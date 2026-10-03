ORBITAL — laboratorium statku / semi_goal

Uruchomienie na Linuksie
Gotowy program: build/linux/Orbital.x86_64
Gra domyślnie zajmuje cały ekran; F11 przełącza tryb okna.
Uruchom go z menedżera plików lub poleceniem:
    ./build/linux/Orbital.x86_64
Zachowaj cały katalog build/linux razem z katalogiem bibliotek data_Orbital_linuxbsd_x86_64.
Wersja wyeksportowana zawiera środowisko .NET; nie wymaga edytora Godota ani SDK.
Cały pakiet do przenoszenia: build/Orbital-semi_goal-linux-x86_64.tar.gz.

Uruchomienie projektu ze źródeł:
    ./run.sh
Skrypt buduje projekt, importuje zasoby i uruchamia grę. Wymaga Godota 4.5.2 .NET i SDK .NET 8.
W tym środowisku oba narzędzia są już w .tools. Skrypt uwzględnia też GODOT_BIN i dotnet z PATH.
Pakiety Godota w NuGet.Config wskazują lokalny katalog GodotSharp/Tools/nupkgs oraz oficjalny NuGet.
Przy odtwarzaniu środowiska pobierz oficjalny Godot 4.5.2 .NET dla Linux x86_64 do .tools
(katalog Godot_v4.5.2-stable_mono_linux_x86_64) i zainstaluj SDK .NET 8.
Plik Orbital.sln można otworzyć w edytorze C#. project.godot otwiera projekt w Godocie .NET.

Sterowanie domyślne
WASD — ruch; mysz — celowanie.
LPM — blaster, PPM — rakieta. Kliknięcie strzela raz, przytrzymanie ponawia ostrzał.
Spacja — zryw, jeśli zainstalujesz i podłączysz silniki zrywu.
Tab / przycisk Statek — stocznia i pauza.
Esc — ustawienia i pauza. F11 — przełączenie pełnego ekranu.
W ustawieniach można zmienić przypisania na klawiaturę lub przyciski myszy.
Esc anuluje oczekiwanie na nowe przypisanie. Zajęte klawisze są odrzucane z komunikatem.
Ustawienia sterowania, głośności i wybrany utwór zapisują się w user://settings.cfg Godota.
Układ modułów, ulepszenia i stan statku nie są zapisywane — każde uruchomienie zaczyna od nowa.

Stocznia
Kliknij moduł w katalogu, a następnie wolne miejsce na siatce. R obraca podgląd, PPM anuluje.
Kliknięcie istniejącego modułu pokazuje jego priorytet, stan, poziom i parametry.
Przeciąganie przenosi moduł; R obraca zaznaczony moduł; Delete go usuwa.
Q podnosi moduł spod kursora bez klikania; LPM odkłada go w wolnym miejscu, R obraca.
Esc lub PPM podczas przenoszenia anuluje zmianę. Nieudane odłożenie pozwala wskazać inne miejsce.
Przeniesienie zachowuje ulepszenia, priorytet i konfigurację broni. Kable pozostają na siatce.
Poza przenoszeniem PPM usuwa wskazany moduł lub kable wraz z mostkiem z jednego pola, bez potwierdzenia.
Skrót podnoszenia można zmienić w ustawieniach. Może dzielić klawisz z akcją używaną tylko podczas lotu.
Wcześniejsze ustawienia są zachowane; przy konflikcie Q z menu/panelem nowy skrót otrzyma wolne E lub T.
Wszystkie moduły i ulepszenia są bezpłatne. Ograniczeniem rozmieszczenia jest siatka 16 × 10.
Osłona dodaje też sterownik, jeśli jeszcze go nie masz i na siatce jest miejsce.
Sterownik osłon używa roboczo ikony emitera z oznaczeniem C.

Narzędzia 1–5: wybór, energia, dane, mostek, usuwanie.
Pomarańczowy port i kabel to zasilanie, turkusowy to sterowanie.
W trybie kabla przeciągnięcie między modułami wyznacza trasę automatycznie.
Przeciągnięcie przez wolne pola rysuje własną trasę, zatwierdzaną po puszczeniu przycisku.
Ten sam typ kabli łączy się na skrzyżowaniu. Mostek rozdziela trasę pionową i poziomą.
Na mostku kabel musi przechodzić prosto. Dwa typy kabla krzyżują się na mostku.
Kable nie mogą przechodzić przez zajęte pola modułów. Port musi mieć dostępną trasę.
Przycisk „Połącz automatycznie” jest tylko w wersji testowej. W finalnej grze go nie będzie.
Automatyczne łączenie zachowuje istniejące kable; nie zawsze znajdzie trasę w ciasnym układzie.
Jeśli port wypada nad rozgałęzieniem innego kabla, przesuń/obróć moduł albo zmień trasę kabla.
Zmiana położenia modułu pozostawia stare kable. Sprawdź komunikat inspektora i nowe porty.

Dowodzenie musi mieć sygnał do sterowników. Każdy odbiorca potrzebuje właściwego sterownika,
a sterowniki także zasilania. Generator główny działa niezależnie.
Każdą broń można przypisać do jednego z dwóch kanałów lub ustawić jako pasywną.
Po przypisaniu drugiej broni do zajętego kanału poprzednia zostaje nieprzypisana.
Seria pasywna o czasie 0 to ciągły ostrzał; podstawowy odstęp między pociskami nadal obowiązuje.

Arena i narzędzia testowe
Na arenie celowo nie ma przeciwników. Rakiety eksplodują w zapamiętanym miejscu kursora.
Konsola pozwala zadać 25 obrażeń, naprawić kadłub, naładować/rozładować baterie i odtworzyć start.
Można naprawić zniszczony statek — to ułatwienie tej wersji testowej.
Zielony HUD pokazuje zapas baterii; żółty znacznik i liczba pokazują zużycie E/s.
Skala żółtego znacznika wynosi roboczo 0–100 E/s; powyżej 100 liczba nadal pokazuje faktyczny pobór.
Wyświetlany pobór jest wygładzony, by pojedyncze strzały nie powodowały nieczytelnych skoków.
Odłączona bateria zachowuje swój zapas, ale nie zasila odbiorców poza swoją działającą siecią.
Przy niedoborze najpierw działa zapas i zasilanie awaryjne, potem priorytety oraz długości kabli.
Całkowity remis odłącza całą grupę. Powrót jest automatyczny bez dodatkowego progu rezerwy.

Testy i budowa
    ./test.sh           — testy modelu bez interfejsu
    ./test.sh --ui      — także test okna, kliknięć, sterowania i zrzuty ekranów; wymaga sesji graficznej
Scenariusz UI używa odizolowanych ustawień w .tools/test-user, nie nadpisuje ustawień gracza.
Logi i zrzuty trafiają do artifacts (nie są commitowane).
Eksport: preset Linux w export_presets.cfg. Wymaga oficjalnych szablonów .NET 4.5.2.
Lokalne szablony są w .tools/export/templates. Wynik: build/linux (nie jest commitowany).

Struktura kodu
Domain — model bez Godota: katalog, siatka i porty, grafy kabli, energia, zależności, zegary broni.
Domain/Tests — wykonywalne testy przypadków mechanicznych, bez dodatkowego frameworka.
App — arena, interfejs stoczni, sterowanie, audio i test integracyjny.
Scenes — punkt wejścia projektu. Assets — fonty, ikona i licencje.
assumptions — docelowe zasady gry; goals — plan całości; semi_goal — zakres tej wersji.
Parametry robocze zmienia się w ModuleCatalog w Domain/Model.cs.

Godot jest dostępny na licencji MIT: Assets/Licenses/Godot.txt.
Informacje o bibliotekach silnika: Assets/Licenses/Godot-third-party.txt.
Fonty Space Grotesk i DM Mono: licencje SIL OFL w Assets/Fonts.
Grafiki i oba utwory pochodzą z wcześniejszych etapów projektu i pozostają zachowane.
Roboczy tytuł: ORBITAL. Przeciwnicy, poziomy i ekonomia należą do kolejnego etapu.
