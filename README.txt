ORBITAL — przetrwanie i laboratorium statku
Godot 4.5.2 .NET / C# · Linux x86_64

Uruchomienie na Linuksie
Gotowy program: build/linux/Orbital.x86_64
Gra domyślnie zajmuje cały ekran; F11 przełącza tryb okna.
Uruchom go z menedżera plików lub poleceniem:
    ./build/linux/Orbital.x86_64
Zachowaj cały katalog build/linux razem z katalogiem bibliotek data_Orbital_linuxbsd_x86_64.
Wersja wyeksportowana zawiera środowisko .NET; nie wymaga edytora Godota ani SDK.
Cały aktualny pakiet do przenoszenia: build/Orbital-linux-x86_64.tar.gz.
W menu wybierz „Nowa rozgrywka” lub „Laboratorium / wersja testowa”.
Wcześniejsza samodzielna wersja testowa nadal jest w build/Orbital-semi_goal-linux-x86_64.tar.gz.
Jej dokładne źródła zachowuje tag Git laboratory-before-survival.

Uruchomienie projektu ze źródeł:
    ./run.sh
Bezpośrednio do laboratorium:
    ./run.sh -- --lab
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
Tab / przycisk Statek — stocznia i pauza w laboratorium.
W pełnej grze stocznia otwiera się wyłącznie po awansie; Tab pozwala wrócić z niej do lotu.
Esc — ustawienia i pauza. F11 — przełączenie pełnego ekranu.
W ustawieniach można zmienić przypisania na klawiaturę lub przyciski myszy.
Esc anuluje oczekiwanie na nowe przypisanie. Zajęte klawisze są odrzucane z komunikatem.
Ustawienia sterowania, głośności i wybrany utwór zapisują się w user://settings.cfg Godota.
Układ modułów, ulepszenia i stan statku nie są zapisywane — każde uruchomienie zaczyna od nowa.

Pełna rozgrywka
Pięć rodzajów wrogów nadciąga bez przerw: pościg, wachlarz, rakiety przewidujące ruch,
statek z orbitującą eskortą i pociski poruszające się w pętlach. Typy dochodzą stopniowo;
po 52 sekundach wszystkie mogą się pojawiać. Tempo napływu, ostrzału i zdrowie rosną z czasem.
Celuj w statek macierzysty, żeby zniszczyć też jego eskortę i otrzymać nagrody za miniony.
Czerwone okręgi oznaczają strefy rakiet przeciwnika. Jasny punkt na statku to środek trafienia.

EXP oraz materiały wpadają automatycznie za zabójstwa. Pasek pod nagłówkiem pokazuje postęp,
a boczny HUD — poziom, materiały, czas i liczbę zabójstw. Każdy awans zatrzymuje walkę.
Sklep daje 3 losowe oferty po jednym egzemplarzu. Kupione moduły trafiają do magazynu.
Wybierz moduł na siatce i użyj „Ulepsz +1”: jedno ulepszenie na awans jest całkowicie darmowe.
Pominięcie wymaga potwierdzenia; niewykorzystane ulepszenie przepada. Nadmiar EXP zostaje.
Na poziomach 5, 10, 15 itd. siatka rośnie o 2 kolumny i 2 wiersze. Przesuwaj ją kursorem
przy krawędzi widoku albo paskami przewijania. Po śmierci cały postęp lotu przepada.

Stocznia
W pełnej grze kliknij moduł z magazynu, a w laboratorium z darmowego katalogu,
a następnie wolne miejsce na siatce. R obraca podgląd, PPM anuluje.
Kliknięcie istniejącego modułu pokazuje jego priorytet, stan, poziom i parametry.
Przeciąganie przenosi moduł; R obraca zaznaczony moduł; Delete go usuwa.
Q podnosi moduł spod kursora bez klikania; LPM odkłada go w wolnym miejscu, R obraca.
Esc lub PPM podczas przenoszenia anuluje zmianę. Nieudane odłożenie pozwala wskazać inne miejsce.
Przeniesienie zachowuje ulepszenia, priorytet i konfigurację broni. Kable pozostają na siatce.
Poza przenoszeniem PPM usuwa wskazany moduł lub kable wraz z mostkiem z jednego pola, bez potwierdzenia.
Skrót podnoszenia można zmienić w ustawieniach. Może dzielić klawisz z akcją używaną tylko podczas lotu.
Wcześniejsze ustawienia są zachowane; przy konflikcie Q z menu/panelem nowy skrót otrzyma wolne E lub T.
W pełnej grze usunięty moduł wraca do magazynu. Zachowuje ustawienia, ulepszenia i zapasy;
zdejmowanie baterii lub osłon nie ładuje ich za darmo. Zakup osłony obejmuje jej sterownik.
Wyłącznie w laboratorium wszystkie moduły i ulepszenia są bezpłatne, a siatka ma 18 × 12.
Laboratorium przy dodawaniu osłony umieszcza też brakujący sterownik, jeśli jest miejsce.

Narzędzia 1–5: wybór, energia, dane, mostek, usuwanie.
Pomarańczowy port i kabel to prąd. Dane mają osobne sieci:
- turkusowy: dowodzenie → wejścia sterowników;
- niebieski: sterownik napędu → silniki i zryw;
- zielony: sterownik zasilania → baterie i dodatkowe generatory;
- koralowy: sterownik uzbrojenia → wszystkie bronie;
- fioletowy: sterownik osłon → emitery osłon.
Sterownik ma trzy złącza: prąd, wejście dowodzenia i wyjście własnego systemu.
W trybie „3 Dane” wybierz typ sieci z listy. Przy rozpoczęciu na module typ jest wybierany
według najbliższego złącza danych, dlatego zacznij przeciąganie przy właściwym porcie.
Inspektor i podpowiedzi opisują wejścia i wyjścia. Generator główny nie ma złącza danych.
Różne sieci nie mogą się łączyć; błędna operacja wyświetla komunikat i niczego nie zmienia.
Mostek pozwala im się skrzyżować. Nie można usunąć samego mostka, jeśli pomieszałoby to sieci.
W trybie kabla przeciągnięcie między modułami wyznacza trasę automatycznie.
Przeciągnięcie przez wolne pola rysuje własną trasę, zatwierdzaną po puszczeniu przycisku.
Ten sam typ kabli łączy się na skrzyżowaniu. Mostek rozdziela trasę pionową i poziomą.
Na mostku kabel musi przechodzić prosto. Dwa typy kabla krzyżują się na mostku.
Kable nie mogą przechodzić przez zajęte pola modułów. Port musi mieć dostępną trasę.
Przycisk „Połącz automatycznie” jest tylko w laboratorium; pełna gra go nie udostępnia.
Automatyczne łączenie zachowuje istniejące kable; nie zawsze znajdzie trasę w ciasnym układzie.
Jeśli port wypada nad rozgałęzieniem innego kabla, przesuń/obróć moduł albo zmień trasę kabla.
Zmiana położenia modułu pozostawia stare kable. Sprawdź komunikat inspektora i nowe porty.

Dowodzenie musi mieć sygnał do sterowników. Każdy odbiorca potrzebuje właściwego sterownika,
a sterowniki także zasilania. Generator główny działa niezależnie.
Każdą broń można przypisać do jednego z dwóch kanałów lub ustawić jako pasywną.
Po przypisaniu drugiej broni do zajętego kanału poprzednia zostaje nieprzypisana.
Seria pasywna o czasie 0 to ciągły ostrzał; podstawowy odstęp między pociskami nadal obowiązuje.

Uzbrojenie — wszystkie 7 rodzajów może działać aktywnie lub w seriach pasywnych
- Blaster: szybki strumień pocisków, niskie zużycie energii.
- Rakieta: eksploduje w zapamiętanym punkcie kursora albo po trafieniu wroga.
- Rakieta sterowana: śledzi aktualny kursor aż do dotarcia lub trafienia.
- Laser: ciągła wiązka rażąca wszystkich wrogów na linii strzału.
- Wachlarz: pięć pocisków, środkowy skierowany ku kursorowi.
- Bomba: zostaje w miejscu statku, wybucha po 0,85 s; nie rani gracza.
- Pocisk samonaprowadzający: wybiera najbliższego wroga, po jego śmierci szuka następnego.
Bez celu leci dalej, szukając kolejnego; znika po 9 s lub opuszczeniu obszaru symulacji.
Na start masz blaster i rakietę. Pozostałe bronie oraz dodatkowe systemy kupujesz po awansach.

Laboratorium i energia
Arena laboratorium pozostaje pusta; panel dostępny jest w dowolnym momencie.
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
    ./test.sh --ui      — także scenariusze UI laboratorium i pełnej gry; wymaga sesji graficznej
    ./test.sh --game    — model oraz sam scenariusz UI pełnej gry
51 testów modelu obejmuje również wszystkie bronie, pięć typów wrogów, nagrody, sklep,
bezpłatne ulepszenie, wzrost siatki, magazyn, długą walkę i gęsty ostrzał.
Scenariusz UI używa odizolowanych ustawień w .tools/test-user, nie nadpisuje ustawień gracza.
Logi i zrzuty trafiają do artifacts (nie są commitowane).
Weryfikacja wydania 2026-10-04: 51/51 testów, oba scenariusze UI także w samodzielnej
aplikacji spoza projektu bez SDK; kompilacja bez błędów i ostrzeżeń.
Eksport: preset Linux w export_presets.cfg. Wymaga oficjalnych szablonów .NET 4.5.2.
Lokalne szablony są w .tools/export/templates. Wynik: build/linux (nie jest commitowany).

Struktura kodu
Domain — model bez Godota: katalog, siatka, grafy kabli, energia, zegary broni, walka i postęp lotu.
Domain/Tests — wykonywalne testy przypadków mechanicznych, bez dodatkowego frameworka.
App — arena, menu, sklep i stocznia, sterowanie, audio i scenariusze integracyjne.
Scenes — punkt wejścia projektu. Assets — fonty, ikony nowych modułów i licencje.
assumptions — zasady i decyzje wykonawcze; goals — wykonany zakres; semi_goal — zachowane laboratorium.
Parametry robocze:
- Domain/Model.cs — katalog 19 modułów, rozmiary, energia, parametry 7 broni;
- Domain/ShipSimulation.cs — energia, skalowanie ulepszeń, napęd i sterowanie;
- Domain/CombatWorld.cs — przeciwnicy, ostrzał, nagrody, napływ i kolizje;
- Domain/RunProgress.cs — progi poziomów, ceny, oferty, magazyn i darmowe ulepszenie.
Kompilacja: ./run.sh lub dotnet build Orbital.csproj. Testy modelu nie zależą od Godota.

Godot jest dostępny na licencji MIT: Assets/Licenses/Godot.txt.
Informacje o bibliotekach silnika: Assets/Licenses/Godot-third-party.txt.
Fonty Space Grotesk i DM Mono: licencje SIL OFL w Assets/Fonts.
Grafiki i oba utwory pochodzą z wcześniejszych etapów projektu i pozostają zachowane.
Roboczy tytuł: ORBITAL. Parametry trudności i cen można dalej dostrajać po graniu.
