# LexiGo — exact PDF text

Extracted from the user-supplied PDF; original PDF is authoritative for layout.

## PDF page 1

LexiGo - ენების სასწავლო პლატფორმა
სადიპლომო პროექტის დოკუმენტაცია
ინგლისურიდან ესპანური, იტალიური, გერმანული და რუსული ენების შესწავლა
27 ოქტომბერი, 2026
1. პროექტის მიზანი და დანიშნულება
პროექტის მიზანია შეიქმნას მრავალენოვანი საგანმანათლებლო ვებპლატფორმა, რომელიც აერთიანებს
სტრუქტურირებულ ენის კურსებს, ინტერაქტიულ გაკვეთილებს, პერსონალიზებულ პრაქტიკას,
პროგრესის ანალიზსა და სწავლის მოტივაციის ინსტრუმენტებს. პლატფორმა ემსახურება
დამოუკიდებლად სწავლის მსურველებს, მასწავლებლებსა და სასწავლო ჯგუფებს.
ინტერფეისის, ინსტრუქციების, თარგმანებისა და გრამატიკული განმარტებების საწყისი ენაა
ინგლისური. სასწავლო მიმართულებებია English to Spanish, English to Italian, English to German და
English to Russian. თითოეულ მიმართულებას აქვს საკუთარი მასალა, გაკვეთილების თანმიმდევრობა,
ლექსიკონი, დიალოგები და ინდივიდუალური პროგრესი.
სწავლა ეფუძნება მოკლე, თემატურ გაკვეთილებს. მომხმარებელი ასრულებს მრავალფეროვან
სავარჯიშოებს, იღებს მყისიერ უკუკავშირს, აგროვებს გამოცდილების ქულებს და უბრუნდება
მასალას, რომლის განმტკიცებაც სჭირდება. სისტემა აკავშირებს შედეგებს ყოველდღიურ
აქტივობასთან, პირად მიზნებთან და მიღწევებთან.
პროექტის ძირითადი ამოცანები
• ოთხი ენის შესწავლა ერთიანი, რესფონსიული ინტერფეისით.
• მასალის მართვა ავტორების, რეცენზენტებისა და ადმინისტრატორების მონაწილეობით.
• პასუხების უსაფრთხო სერვერული შეფასება და ისტორიის შენახვა.
• შეცდომების ანალიზითა და ინტერვალებით გამეორებით პერსონალიზებული პრაქტიკა.
• კლასების, დავალებებისა და ჯგუფური პროგრესის მართვა.
• როლებზე დაფუძნებული წვდომა და ანგარიშვალდებული ადმინისტრირება.
მომხმარებლის ძირითადი გზა
რეგისტრაცია -> ენისა და მიზნის არჩევა -> საწყისი ცოდნის შემოწმება -> სასწავლო რუკა -> გაკვეთილი
-> შედეგები -> შეცდომების გამეორება -> ლექსიკონი და პრაქტიკული დიალოგები.

## PDF page 2

2. ტექნოლოგიები და არქიტექტურა
Front-end
• Angular / TypeScript: standalone კომპონენტები, Angular Router, Reactive Forms და HttpClient.
• HTML5 / CSS3 / SCSS: CSS Grid, Flexbox, თემის ცვლადები და რესფონსიული განლაგება.
• Angular Signals: მომხმარებლის, კურსის, სესიისა და შეტყობინებების მდგომარეობა. RxJS: HTTP,
ძიების დაყოვნება და ასინქრონული ნაკადები.
• Angular CDK: ხელმისაწვდომი დიალოგები; Chart.js: პროგრესისა და აქტივობის დიაგრამები.
• HTML Audio API: მოსმენა; MediaRecorder API: საკუთარი ხმის ჩაწერა და მოსმენა.
• Vitest: კომპონენტებისა და სერვისების ტესტები; Playwright: სრული სცენარების ტესტები.
Back-end
• C# / ASP.NET Core Web API (.NET 10): REST, dependency injection და async ოპერაციები.
• Entity Framework Core 10 / Npgsql: PostgreSQL, Code First და მიგრაციები.
• ASP.NET Core Identity: მომხმარებლები, როლები და პაროლების ჰეშირება.
• JWT Bearer და როტირებადი refresh token: ავტორიზაცია და სესიის განახლება.
• Swagger / OpenAPI: კონტრაქტები, DTO სქემები და ავტორიზებული გამოცდა.
• ProblemDetails, ILogger და rate limiting: შეცდომები, ლოგირება და მოთხოვნების შეზღუდვა.
• xUnit, FluentAssertions, WebApplicationFactory და Testcontainers PostgreSQL: ტესტირება.
ინფრასტრუქტურა და დამხმარე სერვისები
PostgreSQL ინახავს მონაცემებს. S3-თავსებადი საცავი ინახავს აუდიოფაილებს, ბაზა კი მათ
მეტამონაცემებს. SMTP უზრუნველყოფს ელფოსტის დადასტურებასა და პაროლის აღდგენას. პირადი
ფაილებისთვის გამოიყენება დროებითი დაცული ბმულები.
Git / GitHub: ვერსიები და კოდის მიმოხილვა; GitHub Actions: build, lint და ტესტები; Docker / Docker
Compose: ერთიანი გარემო. ჰოსტინგი: DigitalOcean Droplet, Ubuntu, Nginx და HTTPS. სარეზერვო
ასლები იქმნება გრაფიკით. პაკეტების ვერსიები ფიქსირდება პროექტის ფაილებსა და lockfile-ში.

## PDF page 3

3. როლები და უფლებები
სტუმარი (Guest)
ათვალიერებს მთავარ გვერდს, ოთხი კურსის მიმოხილვას, დახმარებასა და საცდელ გაკვეთილს.
საცდელი სესია არ ქმნის რეიტინგის ქულებს. პროგრესის შესანახად რეგისტრირდება.
მსწავლელი (Learner)
ირჩევს კურსებს, ასრულებს გაკვეთილებსა და დიალოგებს, იყენებს ლექსიკონს, მიზნებსა და
შეხსენებებს, იღებს მიღწევებს, მონაწილეობს რეიტინგში და უერთდება კლასს მოწვევით.
მასწავლებელი (Teacher)
ქმნის კლასებს, გასცემს მოწვევის კოდს, ანაწილებს გამოქვეყნებულ გაკვეთილებს დავალებებად,
ადგენს ვადას და ხედავს საკუთარი კლასის დავალებების შედეგებს. ტოვებს წერილობით უკუკავშირს.
კლასგარეშე პირადი ისტორია მასწავლებლისთვის მიუწვდომელია.
მასალის ავტორი (Content Author)
ქმნის საკუთარ მონახაზებს: გაკვეთილებს, სავარჯიშოებს, სიტყვებს, აუდიოსა და დიალოგებს.
აგზავნის რეცენზირებისთვის და ასწორებს შენიშვნებს. დამოუკიდებლად არ აქვეყნებს საჯარო
მასალას.
რეცენზენტი (Reviewer)
ამოწმებს მისთვის მინიჭებული ენების თარგმანს, გრამატიკას, პასუხებს, განმარტებებსა და აუდიოს.
ამტკიცებს ან შენიშვნით აბრუნებს ვერსიას. საკუთარი მასალის საბოლოო დამტკიცება დაუშვებელია.
მოდერატორი (Moderator)
განიხილავს შეცდომების შეტყობინებებს, საჩივრებსა და არასათანადო საჯარო სახელებს; აფიქსირებს
გადაწყვეტილებასა და მიზეზს. არ ცვლის როლებს ან შეფასების გასაღებს.
ადმინისტრატორი (Admin)
მართავს კურსების სტრუქტურას, დამტკიცებული მასალის გამოქვეყნებას, არქივირებასა და
სტატისტიკას. საჭიროებისას ბლოკავს ანგარიშს მიზეზის მითითებით. როლების მინიჭება ცალკე
უფლებას მოითხოვს. BillingManage უფლების ფარგლებში მართავს სატესტო გადახდებს, გეგმებსა და
დასაბუთებულ დაბრუნებებს.
მთავარი ადმინისტრატორი (Super Admin)
მართავს როლებს, უფლებებს, ენების ფარგლებს, ადმინების ანგარიშებს, სისტემურ პარამეტრებსა და
აუდიტს. საწყისი ანგარიში იქმნება დაცული კონფიგურაციით. მომხმარებელს შეიძლება ჰქონდეს
რამდენიმე როლი; უფლებები მოწმდება API-ის შესაბამის მოქმედებებზე.

## PDF page 4

4. ანგარიშები და უსაფრთხოება
ანგარიშის ფუნქციები
• რეგისტრაცია სახელით, უნიკალური ელფოსტითა და პაროლით.
• ელფოსტის დადასტურება ვადიანი, ერთჯერადი ბმულით.
• შესვლა, გამოსვლა, ტოკენის განახლება და აქტიური სესიების გაუქმება.
• პაროლის აღდგენა ელფოსტით; პაროლისა და ელფოსტის შეცვლა დადასტურებით.
• განმეორებითი არასწორი შესვლის დროებითი შეზღუდვა.
პროფილის პარამეტრები
• საჩვენებელი სახელი, ავატარი, დროის სარტყელი და აქტიური კურსი.
• დღიური XP მიზანი, სასწავლო დღეები და შეხსენების დრო.
• ღია/მუქი თემა, ხმები და ანიმაციების შემცირება.
• ლიდერბორდში გამოჩენისა და ელფოსტის შეხსენებების არჩევა.
• სასწავლო მონაცემების ექსპორტი და ანგარიშის წაშლა.
ტექნიკური დაცვა
პაროლი ინახება Identity-ის ჰეშით. Access token ხანმოკლეა; refresh token დაცულ HttpOnly/Secure
cookie-შია, ბაზაში კი მისი ჰეში. განახლებისას ძველი refresh token უქმდება. API ამოწმებს როლს, ენის
ფარგლებს, რესურსის მფლობელსა და კლასის წევრობას.
CORS შემოიფარგლება აპლიკაციის დომენით; cookie-ზე დამოკიდებული მოქმედებები დაცულია
CSRF-ისგან. გამოიყენება HTTPS, ფაილის ტიპისა და ზომის შემოწმება, ტექსტის უსაფრთხო
დამუშავება და სერვერული ვალიდაცია. მომხმარებლის შეყვანილი HTML არ სრულდება.
საიდუმლოები ინახება გარემოს ცვლადებსა და User Secrets-ში. ლოგებში არ იწერება პაროლი ან
ტოკენი. ანგარიშის წაშლა აუქმებს სესიებს, შლის პირად მონაცემებს და ანონიმურს ხდის
აგრეგირებულ სტატისტიკას. გამოქვეყნებული მასალის ავტორობის ისტორია ინარჩუნებს
სტრუქტურულ მთლიანობას.

## PDF page 5

5. კურსები და სასწავლო მასალა
ოთხი სასწავლო მიმართულება
• ესპანური: ძირითადი ლექსიკა, არტიკლები, სქესი, ზმნები, ñ და აქცენტიანი ასოები.
• იტალიური: არტიკლები, სახელთა სქესი, ძირითადი ზმნები და ყოველდღიური ფრაზები.
• გერმანული: der/die/das, სიტყვების რიგი, ძირითადი ბრუნვები, ä/ö/ü და ß.
• რუსული: კირილიცა, ასოებისა და ბგერების დაკავშირება, სქესი და ძირითადი ბრუნვები.
ყველა კურსში ინსტრუქციები და განმარტებები ინგლისურ ენაზეა. მასალა იწყება საფუძვლებით და
თანდათან რთულდება. თემებს აქვს სასწავლო მიზანი და სირთულის აღწერა. კურსის პროგრესი არ
ერევა სხვა ენის შედეგებში.
სტრუქტურა და თემები
კურსი იყოფა ერთეულებად; ერთეული აერთიანებს გაკვეთილებს, გრამატიკულ განმარტებებსა და
შემაჯამებელ ტესტს. გაკვეთილი შეიცავს მიზანს, სიტყვებს, მაგალითებს, აუდიოსა და სავარჯიშოებს.
წინაპირობები განსაზღვრავს შემდეგი გაკვეთილის გახსნას.
• ანბანი, გამოთქმის საფუძვლები, მისალმება და საკუთარი თავის წარდგენა.
• რიცხვები, დრო, თარიღი, ფერები და ყოველდღიური ნივთები.
• ოჯახი, ადამიანები, პროფესია და სწავლა.
• საკვები, კაფე, მაღაზია, ფასი და შეკვეთა.
• ქალაქი, ტრანსპორტი, სასტუმრო და მიმართულებები.
• ყოველდღიური რუტინა, ჰობი, ამინდი და გეგმები.
დონის შემოწმება
მომხმარებელს შეუძლია გაიაროს საწყისი დიაგნოსტიკური ტესტი. შედეგი განსაზღვრავს
რეკომენდებულ ერთეულს; საფუძვლები კვლავ ხელმისაწვდომია. ტესტი არ ანიჭებს გამოტოვებული
გაკვეთილების XP-ს. ერთეულის დასასრულს შემაჯამებელი ტესტი ამოწმებს სხვადასხვა უნარს.
გამოქვეყნების პროცესი
მონახაზი -> რეცენზირება -> დამტკიცება -> გამოქვეყნება -> არქივირება. ვალიდაცია ამოწმებს
პასუხებს, აუდიოს, აუცილებელ ველებსა და გადასვლებს. რედაქტირება ქმნის ვერსიას; მიმდინარე
სესიები აგრძელებს დაწყებული ვერსიის გამოყენებას. აუდიოს ახლავს წყაროსა და გამოყენების
უფლების მონაცემები.

## PDF page 6

6. გაკვეთილი და სავარჯიშოები
• Multiple Choice: სწორი სიტყვის, თარგმანის ან ფრაზის არჩევა.
• Matching: სიტყვების თარგმანებთან, აუდიოსთან ან ფრაზებთან დაკავშირება.
• Sentence Builder: მოცემული სიტყვებით წინადადების აწყობა.
• Fill in the Blank: სიტყვის ან ზმნის ფორმის ჩასმა.
• Translation: მოკლე ფრაზის თარგმნა ორივე მიმართულებით.
• Listening: აუდიოს მოსმენა და პასუხის არჩევა ან აკრეფა.
• Reading: მცირე ტექსტი და შინაარსზე კითხვები.
• Alphabet Practice: რუსული ასოების ამოცნობა და ბგერებთან დაკავშირება.
• Speaking Practice: ნიმუშის მოსმენა, ხმის ჩაწერა და შედარებისთვის მოსმენა.
• Dialogue Choice: სიტუაციაში შესაფერისი რეპლიკის არჩევა.
სესიის მიმდინარეობა
დაწყებისას სერვერი აფიქსირებს სავარჯიშოებს, რიგითობასა და მასალის ვერსიას. ეკრანზე ჩანს
პროგრესის ზოლი, გულები, ინსტრუქცია და შემოწმების ღილაკი. პასუხის შემდეგ ბრუნდება შედეგი
და განმარტება. მდგომარეობა ინახება ყოველი პასუხის შემდეგ; კავშირის აღდგენისას სესია
განახლდება.
პასუხების შეფასება
ტექსტური პასუხებისთვის გამოიყენება Unicode ნორმალიზაცია, ზედმეტი გამოტოვებების მოცილება
და დავალების რეგისტრის/პუნქტუაციის წესი. მნიშვნელობის მქონე ასოები არ ჩანაცვლდება ბრმად.
ალტერნატიული სწორი პასუხები ცალკე ინახება. აწყობისას მოწმდება ტოკენების რიგი,
დაკავშირებისას - წყვილები. სწორი პასუხის გასაღები კლიენტს წინასწარ არ გადაეცემა.
ქულებს ითვლის სერვერი. ხმის ჩაწერა ემსახურება თვითშეფასებას: მომხმარებელი უსმენს საკუთარ
ჩანაწერსა და ნიმუშს. ჩაწერის დასრულება არ ანიჭებს ავტომატურ გამოთქმის სიზუსტის ქულას. ხმა
ნაგულისხმევად მუშავდება ბრაუზერში და მოსმენის შემდეგ თავისუფლდება.
შედეგი და გამეორება
დასრულებისას ჩანს სიზუსტე, პასუხების რაოდენობა, XP, დრო, შეცდომები და შემდეგი ნაბიჯი.
შესაძლებელია შეცდომების პრაქტიკა, გაკვეთილის გამეორება ან შემდეგზე გადასვლა. გულების
ამოწურვისას ხელმისაწვდომია უფასო გამეორებითი პრაქტიკა.

## PDF page 7

7. პერსონალიზაცია და საკუთარი დამატებები
შეცდომებზე მორგებული პრაქტიკა
შეცდომები უკავშირდება თემებსა და უნარებს: ლექსიკა, გრამატიკა, მოსმენა, კითხვა და ანბანი.
სისტემა ითვალისწინებს შეცდომების სიხშირეს, ბოლო მცდელობასა და წარმატებულ გამეორებებს.
პირველ რიგში ირჩევა სუსტი თემები, შემდეგ - ძველი მასალა. იზღუდება ერთი ტიპის დავალებების
ზედიზედ რაოდენობა.
მომხმარებელი ხედავს რეკომენდაციის მიზეზს: ხშირი შეცდომა ან გამეორების ვადის დადგომა.
შედეგების მიხედვით იცვლება პრაქტიკის სირთულე. პირველად შეუმოწმებელი თემა ათვისებულად
არ ჩაითვლება.
პირადი ლექსიკონი და ინტერვალებით გამეორება
სიტყვას აქვს თარგმანი, მაგალითი, ენა, აუდიო და თემა. მომხმარებელი ინახავს სიტყვებს პირად
ლექსიკონში და აჯგუფებს კოლექციებად. ძიება მუშაობს სასწავლო სიტყვითა და ინგლისური
თარგმანით; ფილტრებია ენა, თემა და გამეორების სტატუსი.
სწორი პასუხი ზრდის ინტერვალს: 1, 3, 7, 14 და 30 დღე. შეცდომა აბრუნებს სიტყვას მოკლე
ინტერვალზე. მიმოხილვის ისტორია ინახება; ვადაგასული სიტყვები ჩანს დღევანდელ სიაში.
დუბლიკატი სიტყვა და განმეორებითი მოთხოვნა დამატებით ჯილდოს არ ქმნის.
განშტოებული სიტუაციური დიალოგები
კაფე, სასტუმრო, ბილეთის შეძენა და მიმართულების კითხვა წარმოდგენილია ოთხივე ენაზე.
რეპლიკის არჩევანი განსაზღვრავს მომდევნო კვანძს. განმარტება ხსნის სიტყვების შერჩევასა და
თავაზიანობის ფორმას. დასრულებისას ჩანს გავლილი გზა და გასამეორებელი სიტყვები.
უნარების რუკა და დამხმარე ინსტრუმენტები
თემის ინდიკატორი აჩვენებს ათვისებას, ბოლო პრაქტიკასა და სუსტ მხარეებს. რუკიდან
შესაძლებელია შესაბამისი პრაქტიკის დაწყება. ენის სპეციალური სიმბოლოების ეკრანული
კლავიატურა ეხმარება აკრეფას. კვირის შეჯამება აჩვენებს გაკვეთილებს, სიტყვებს, აქტიურ დღეებსა
და რეკომენდაციებს.
მიღწევის ბარათი
ერთეულის დასრულებისას იქმნება შიდა ციფრული ბარათი სახელით, კურსით, დასრულებული
ერთეულითა და შედეგით. ბარათს აქვს უნიკალური კოდი და გაზიარების არჩევანი; მისი შინაარსი
ასახავს პლატფორმაზე შესრულებულ სასწავლო სამუშაოს.

## PDF page 8

8. მოტივაცია, შეტყობინებები და კლასები
XP, streak და გამოწვევები
ახალი გაკვეთილის, გამეორებისა და დიალოგის XP განისაზღვრება ცალკე წესებით. Streak იზრდება
დღიური მიზნის შესრულებისას მომხმარებლის დროის სარტყლით. მიღწევებია პირველი გაკვეთილი,
ზედიზედ აქტიური დღეები, სიტყვების კოლექცია და უნაკლო სესია. გამოწვევები აერთიანებს
მიზნობრივ გაკვეთილებსა და აქტივობას; ჯილდო გაიცემა ერთხელ.
ლიდერბორდი
რეიტინგი გამოითვლება კვირისა და კურსის მიხედვით. ჩანს სახელი, ავატარი და XP. მონაწილეობა
არჩევითია. თანაბარი XP-ისას რიგითობა განისაზღვრება მიღწევის დროით. პირადი ელფოსტა და
სრული სასწავლო ისტორია რეიტინგში არ ქვეყნდება.
შეტყობინებები და შეხსენებები
შიდა შეტყობინებები მოიცავს მიღწევას, ახალ დავალებას, მოახლოებულ ვადასა და საჩივრის შედეგს.
ელფოსტის შეხსენებები იგზავნება მომხმარებლის არჩევანით. ფონური სერვისი ამოწმებს დროის
სარტყელსა და გრაფიკს; გაგზავნის ჩანაწერი გამორიცხავს დუბლიკატს. მომხმარებელი მართავს არხსა
და სიხშირეს.
კლასები და დავალებები
მასწავლებელი ქმნის კლასს სახელით, ენითა და აღწერით. წევრი ერთიანდება ვადიანი მოწვევის
კოდით. შესაძლებელია მოწვევის განახლება, წევრის ამოღება და კლასის არქივირება. სტუდენტი
ხედავს საკუთარ კლასებს და შეუძლია გასვლა.
დავალება მოიცავს ერთ ან რამდენიმე გამოქვეყნებულ გაკვეთილს, ვადასა და მინიმალურ შედეგს.
მასწავლებელი ხედავს შესრულების სტატუსს, მცდელობებს, სიზუსტესა და დაგვიანებას და ტოვებს
უკუკავშირს. დამოუკიდებელი პრაქტიკის შედეგის მიბმა დავალებაზე ხდება მისი წესით. სხვა კლასის
ან მომხმარებლის შედეგები მიუწვდომელია.
საჩივრები და მასალის შეცდომები
მომხმარებელი აგზავნის სავარჯიშოს შეცდომას, გაუმართავ აუდიოს ან ტექსტურ ხარვეზს. ჩანაწერს
ახლავს რესურსის ვერსია და კომენტარი. მოდერატორი ან ენის რეცენზენტი აფიქსირებს სტატუსს:
ახალი, განხილვაში, გადაწყვეტილი ან უარყოფილი; განმცხადებელი იღებს შედეგს.

## PDF page 9

9. ადმინისტრირება და სამუშაო პროცესები
კონტენტის მართვა
• კურსების, ერთეულების, გაკვეთილების შექმნა, დალაგება, წინასწარი ნახვა და არქივირება.
• ტიპის შესაბამისი სავარჯიშოს ფორმები: ვარიანტები, წყვილები, ტოკენები და პასუხები.
• ლექსიკის, გრამატიკის, უნარების ტეგებისა და აუდიომასალის მართვა.
• დიალოგების კვანძები და გადასვლები; ჩიხებისა და არასწორი კავშირების შემოწმება.
• CSV იმპორტი შაბლონით, წინასწარი ვალიდაცია და შეცდომების ანგარიში.
• მონახაზის დუბლირება ახალი გაკვეთილისთვის; ვერსიების ისტორია.
რეცენზირება და გამოქვეყნება
ავტორი წარადგენს ვერსიას. რეცენზენტი ამოწმებს მისთვის მინიჭებული ენის მასალას და ტოვებს
შენიშვნებს. დამტკიცების შემდეგ ადმინისტრატორი აქვეყნებს მას. ცვლილება არ ცვლის დაწყებული
სესიის გასაღებს. ობიექტი ინახავს ავტორს, დამმტკიცებელს, დროსა და ვერსიას.
მომხმარებლები და უფლებები
ადმინისტრატორი ეძებს მომხმარებლებს, ფილტრავს როლითა და სტატუსით, ბლოკავს/აღადგენს
ანგარიშს და ხედავს ადმინისტრაციულ ისტორიას. Super Admin ანიჭებს როლებსა და ენის ფარგლებს.
ბოლო აქტიური Super Admin-ის უფლებების გაუქმება იკრძალება.
ანალიტიკა და ექსპორტი
პანელი აჩვენებს აქტიურ მომხმარებლებს, კურსების ჩართულობას, გაკვეთილების დასრულებას,
სავარჯიშოთა სირთულესა და ხშირ შეცდომებს. ფილტრებია კურსი და თარიღების დიაპაზონი. CSV
ექსპორტი შეიცავს მხოლოდ როლისთვის ნებადართულ ველებს.
აუდიტი და პარამეტრები
როლის ცვლილება, ბლოკირება, დამტკიცება/გამოქვეყნება, იმპორტი და მნიშვნელოვანი
კონფიგურაციის ცვლილება იწერება აუდიტში: მოქმედი პირი, ობიექტი, მოქმედება, დრო და მიზეზი.
ჟურნალი ცალკეა ტექნიკური ლოგებისგან. პარამეტრებია XP წესები, მიზნები, ფაილის ლიმიტები და
შეხსენებების გრაფიკი.

## PDF page 10

10. Front-end და State management
ძირითადი გვერდები
მთავარი; კურსების კატალოგი; რეგისტრაცია/შესვლა; დადასტურება/აღდგენა; საწყისი გამართვა;
სასწავლო რუკა; გაკვეთილი; შედეგები; პრაქტიკა; ლექსიკონი; დიალოგი; პროგრესის პანელი;
მიღწევები; ლიდერბორდი; კლასები/დავალებები; შეტყობინებები; პროფილი; Free/Premium გეგმები;
სატესტო ბარათები; გადახდები და ისტორია; ადმინისტრირება.
src/app/
  core/       auth, guards, interceptors, api, errors
  shared/     buttons, dialogs, cards, forms, audio-player
  features/
    auth/          login, register, recovery
    onboarding/    language, goals, placement
    courses/       catalog, learning-path
    learning/      session, exercise-renderer, results
    exercises/     choice, matching, builder, listening
    practice/      mistakes, review-queue
    vocabulary/    words, collections, review
    dialogues/     scenarios, conversation
    progress/      dashboard, achievements, leaderboard
    classrooms/    classes, assignments, feedback
    notifications/ inbox, preferences
    profile/       account, settings
    admin/         content, reviews, users, reports
მდგომარეობის მართვა
AuthStore ინახავს მომხმარებელსა და უფლებებს; CourseStore - კურსსა და რუკას; LearningStore - სესიას,
პასუხებსა და პროგრესს; VocabularyStore - ფილტრებსა და მიმოხილვის რიგს. კომპონენტები
კითხულობს computed მდგომარეობას და იძახებს სერვისის მოქმედებებს. შედეგებისა და უფლებების
სანდო წყარო სერვერია.
მოთხოვნებს აქვს loading, success, empty და error მდგომარეობები. წარუმატებელი გაგზავნა არ ზრდის
XP-ს. ღილაკი დროებით იბლოკება; idempotency key იცავს დუბლიკატისგან. ანგარიშის შეცვლისას
პირადი cache იწმინდება. განახლებისას სესია აღდგება API-დან.
UI/UX და ხელმისაწვდომობა
CSS Grid/Flexbox უზრუნველყოფს ეკრანზე მორგებას. გამოიყენება ნათელი/მუქი თემა, მკაფიო ფოკუსი,
კონტრასტი, label-ები და გასაგები ვალიდაცია. პასუხის შედეგს ფერთან ერთად ტექსტიც ახლავს.
შესაძლებელია კლავიატურით მართვა, აუდიოს გამეორება და მოძრაობის შემცირება. გამოსახულებებს
აქვს ალტერნატიული ტექსტი.

## PDF page 11

11. REST API - ანგარიშები და სწავლა
API იყენებს /api/v1 პრეფიქსს. კერძო რესურსები მოითხოვს JWT-ს. /me მომხმარებელს განსაზღვრავს ტოკენით. საჯარო
ან ერთჯერადი დადასტურების მოქმედებებია register/login/refresh/verify/reset და კატალოგის მიმოხილვა. სიებს აქვს
page/pageSize და ფილტრები.
AUTH:
POST /api/v1/auth/register
POST /api/v1/auth/login
POST /api/v1/auth/refresh
POST /api/v1/auth/logout
POST /api/v1/auth/verify-email
POST /api/v1/auth/resend-verification
POST /api/v1/auth/forgot-password
POST /api/v1/auth/reset-password
ACCOUNT:
GET/PATCH/DELETE /api/v1/me
PUT /api/v1/me/email
PUT /api/v1/me/password
GET/DELETE /api/v1/me/sessions[/{id}]
GET/PUT /api/v1/me/preferences
GET /api/v1/me/export
COURSES:
GET /api/v1/courses
GET /api/v1/courses/{id}
GET /api/v1/courses/{id}/units
GET /api/v1/units/{id}/lessons
GET /api/v1/lessons/{id}
GET/POST /api/v1/me/enrollments
GET /api/v1/me/progress?courseId=
POST /api/v1/courses/{id}/placement-sessions
LEARNING:
POST /api/v1/lessons/{id}/sessions
GET /api/v1/me/learning-sessions/{id}
POST /api/v1/me/learning-sessions/{id}/answers
POST /api/v1/me/learning-sessions/{id}/complete
POST /api/v1/me/practice-sessions
GET /api/v1/me/mistakes?courseId=&skillId=
VOCABULARY:
GET /api/v1/vocabulary?courseId=&search=
GET/POST /api/v1/me/saved-words
DELETE /api/v1/me/saved-words/{id}
POST /api/v1/me/saved-words/{id}/reviews
GET/POST /api/v1/me/collections
PUT/DELETE /api/v1/me/collections/{id}
POST/DELETE /api/v1/me/collections/{id}/words[/{wordId}]
DIALOGUES:
GET /api/v1/dialogues?courseId=
POST /api/v1/dialogues/{id}/sessions
GET /api/v1/me/dialogue-sessions/{id}
POST /api/v1/me/dialogue-sessions/{id}/choices

## PDF page 12

12. REST API - დამატებითი მოდულები
PROGRESS:
GET /api/v1/me/dashboard?courseId=
GET /api/v1/me/activity?from=&to=
GET /api/v1/me/achievements
GET /api/v1/me/challenges
GET /api/v1/leaderboards?courseId=&week=
GET /api/v1/me/completion-cards
NOTIFICATIONS / REPORTS:
GET /api/v1/me/notifications
PATCH /api/v1/me/notifications/{id}/read
POST /api/v1/reports
GET /api/v1/me/reports
CLASSROOMS (Teacher / member):
GET/POST /api/v1/classes
GET/PATCH /api/v1/classes/{id}
POST /api/v1/classes/{id}/invitations         (Teacher)
POST /api/v1/classes/join
DELETE /api/v1/classes/{id}/members/{userId}
GET/POST /api/v1/classes/{id}/assignments
GET/PATCH /api/v1/assignments/{id}
GET /api/v1/assignments/{id}/results          (Teacher)
POST /api/v1/assignments/{id}/feedback        (Teacher)
CONTENT (scoped permission):
GET/POST /api/v1/manage/{resource}
GET/PUT/DELETE /api/v1/manage/{resource}/{id}
POST /api/v1/manage/lessons/{id}/submit
POST /api/v1/manage/lessons/{id}/reviews
POST /api/v1/manage/lessons/{id}/publish
GET /api/v1/manage/lessons/{id}/versions
POST /api/v1/manage/media
DELETE /api/v1/manage/media/{id}
POST /api/v1/manage/imports/validate
POST /api/v1/manage/imports/{id}/commit
ADMIN:
GET /api/v1/admin/users
PATCH /api/v1/admin/users/{id}/status
PUT /api/v1/admin/users/{id}/roles            (Super Admin)
GET/PATCH /api/v1/admin/reports[/{id}]
GET /api/v1/admin/analytics
GET /api/v1/admin/audit-logs                  (Super Admin)
GET/PUT /api/v1/admin/settings               (Super Admin)
HEALTH: GET /health/live ; GET /health/ready
{resource} აერთიანებს ცალკე controller-ებს: courses, units, lessons, exercises, vocabulary, skills, dialogues, achievements და
challenges. [/{id}] მხოლოდ სიისა და ერთეულის მარშრუტების შემოკლებაა. თითოეული მეთოდი და DTO ცალკე
აღიწერება Swagger-ში. კლასის შექმნა/რედაქტირება და დავალების დამატება ეკუთვნის მასწავლებელს; მსწავლელი
კითხულობს მხოლოდ თავისი კლასის ნებადართულ მონაცემებს.

## PDF page 13

13. API კონტრაქტი და ბიზნესწესები
პასუხები და სტატუსები
შექმნა აბრუნებს 201 Created-სა და რესურსის მისამართს; წაკითხვა/განახლება - 200-ს, უპასუხო წაშლა -
204-ს. ვალიდაცია - 400, ავტორიზაციის არქონა - 401, არასაკმარისი უფლება - 403, არარსებული
რესურსი - 404, კონფლიქტი - 409, მოთხოვნების შეზღუდვა - 429, მოულოდნელი შეცდომა - 500.
შეცდომის ფორმატია ProblemDetails: type, title, status, detail, instance, traceId და ველების errors.
გვერდებად დაყოფილი სია შეიცავს items, page, pageSize და totalCount-ს. pageSize-ს აქვს ზედა ზღვარი.
პასუხის გაგზავნის მაგალითი
POST /api/v1/me/learning-sessions/{id}/answers
{
  "sessionExerciseId": "uuid",
  "answerText": "Guten Morgen",
  "selectedOptionIds": [],
  "idempotencyKey": "uuid"
}
200 OK
{
  "isCorrect": true,
  "feedback": "Correct greeting for the morning.",
  "heartsRemaining": 5,
  "nextExerciseId": "uuid"
}
ტრანზაქციები და განმეორებითი მოთხოვნები
სესიის დასრულება ატომურად წერს შედეგს, XP-ს, დღიურ აქტივობასა და მიღწევებს. მოთხოვნის
გასაღები უნიკალურია მომხმარებლისა და ოპერაციის ფარგლებში; გამეორება აბრუნებს არსებულ
შედეგს. ერთდროული დასრულება ვერ ქმნის ორმაგ ჯილდოს. პასუხის სავარჯიშო უნდა ეკუთვნოდეს
აქტიურ სესიას.
ბიზნესწესები
• ჩაკეტილი/გამოუქვეყნებელი გაკვეთილი არ იწყება შესაბამისი უფლების გარეშე.
• მიმდინარე სესია იყენებს შინაარსისა და შეფასების ვერსიის ასლს.
• კურსის შეცვლა არ შლის სხვა ენის პროგრესს.
• ვადის შემდეგ დავალება ინახავს დაგვიანების სტატუსს.
• ახალი გაკვეთილი და გამეორება იყენებს განსხვავებულ XP წესებს.
• ლექსიკონის, გამოწვევისა და კოლექციის დუბლიკატები იზღუდება.
• იმპორტი და მრავალცხრილიანი განახლება გადის ვალიდაციასა და ტრანზაქციას.

## PDF page 14

14. მოდელები - ანგარიშები და კურსები
uuid/int არის იდენტიფიკატორი; ? ნიშნავს არასავალდებულო ველს; -> აღნიშნავს გარე გასაღებს. დრო ინახება UTC-ში.
API იყენებს DTO-ებს და არ აბრუნებს Entity-ს ყველა ველს. Identity-ს აქვს ანგარიშის დადასტურების, აღდგენისა და
უსაფრთხოების დამხმარე ცხრილები.
User { id: uuid, email: string, normalizedEmail: string,
  passwordHash: string, displayName: string, avatarMediaId: uuid?,
  emailConfirmed: bool, status: string, createdAt: datetime }
Role { id: uuid, name: string }
UserRole { userId: uuid -> User, roleId: uuid -> Role }
UserPreference { userId: uuid -> User, timeZone: string,
  dailyGoalXp: int, theme: string, reduceMotion: bool,
  soundEnabled: bool, leaderboardOptIn: bool }
RefreshSession { id: uuid, userId: uuid -> User, tokenHash: string,
  expiresAt: datetime, revokedAt: datetime?, replacedById: uuid? }
Language { id: int, code: string, englishName: string,
  nativeName: string, writingDirection: string }
UserLanguageScope { userId: uuid -> User,
  languageId: int -> Language }
Course { id: uuid, sourceLanguageId: int -> Language,
  targetLanguageId: int -> Language, title: string,
  description: text, isPublished: bool, orderNumber: int }
Unit { id: uuid, courseId: uuid -> Course, title: string,
  description: text, orderNumber: int, difficulty: string }
Lesson { id: uuid, unitId: uuid -> Unit, title: string,
  objective: text, orderNumber: int, status: string,
  currentVersionId: uuid? -> LessonVersion }
LessonPrerequisite { lessonId: uuid -> Lesson,
  prerequisiteLessonId: uuid -> Lesson }
LessonVersion { id: uuid, lessonId: uuid -> Lesson,
  versionNumber: int, explanation: text, createdBy: uuid -> User,
  status: string, publishedAt: datetime? }
ContentReview { id: uuid, lessonVersionId: uuid -> LessonVersion,
  reviewerId: uuid -> User, decision: string, comment: text,
  reviewedAt: datetime }
Skill { id: int, courseId: uuid -> Course,
  name: string, category: string }
Exercise { id: uuid, lessonVersionId: uuid -> LessonVersion,
  type: string, prompt: text, explanation: text, orderNumber: int,
  audioMediaId: uuid? -> MediaAsset, evaluationRules: jsonb }
ExerciseSkill { exerciseId: uuid -> Exercise, skillId: int -> Skill }
ExerciseOption { id: uuid, exerciseId: uuid -> Exercise,
  text: string, orderNumber: int, matchKey: string? }
AcceptedAnswer { id: uuid, exerciseId: uuid -> Exercise,
  answerText: text, orderedTokenIds: jsonb? }
MediaAsset { id: uuid, storageKey: string, mimeType: string,
  byteSize: long, languageId: int? -> Language, source: string,
  license: string, uploadedBy: uuid -> User }

## PDF page 15

15. მოდელები - სწავლა და ლექსიკონი
Enrollment { userId: uuid -> User, courseId: uuid -> Course,
  enrolledAt: datetime, placementLevel: string? }
LessonProgress { userId: uuid -> User, lessonId: uuid -> Lesson,
  status: string, bestScore: int, completedAt: datetime? }
LearningSession { id: uuid, userId: uuid -> User,
  courseId: uuid -> Course, lessonId: uuid? -> Lesson,
  mode: string, hearts: int, startedAt: datetime,
  completedAt: datetime?, score: int, awardedXp: int }
SessionExercise { id: uuid, sessionId: uuid -> LearningSession,
  exerciseId: uuid -> Exercise, orderNumber: int,
  publicSnapshot: jsonb, privateEvaluationSnapshot: jsonb }
AnswerAttempt { id: uuid, sessionExerciseId: uuid -> SessionExercise,
  answerPayload: jsonb, isCorrect: bool, idempotencyKey: uuid,
  submittedAt: datetime }
SkillProgress { userId: uuid -> User, skillId: int -> Skill,
  correctCount: int, incorrectCount: int, lastPracticedAt: datetime? }
VocabularyItem { id: uuid, courseId: uuid -> Course,
  word: string, englishTranslation: string, example: text,
  audioMediaId: uuid? -> MediaAsset, partOfSpeech: string }
SavedWord { id: uuid, userId: uuid -> User,
  vocabularyItemId: uuid -> VocabularyItem,
  dueAt: datetime, intervalDays: int, correctStreak: int }
WordReview { id: uuid, savedWordId: uuid -> SavedWord,
  isCorrect: bool, reviewedAt: datetime, idempotencyKey: uuid }
WordCollection { id: uuid, userId: uuid -> User,
  courseId: uuid -> Course, name: string }
CollectionWord { collectionId: uuid -> WordCollection,
  savedWordId: uuid -> SavedWord }
Dialogue { id: uuid, courseId: uuid -> Course, title: string,
  scenario: string, versionNumber: int, status: string }
DialogueNode { id: uuid, dialogueId: uuid -> Dialogue,
  text: text, audioMediaId: uuid? -> MediaAsset,
  isStart: bool, isTerminal: bool }
DialogueChoice { id: uuid, nodeId: uuid -> DialogueNode,
  text: text, feedback: text, isCorrect: bool,
  nextNodeId: uuid? -> DialogueNode }
DialogueSession { id: uuid, userId: uuid -> User,
  dialogueId: uuid -> Dialogue, currentNodeId: uuid -> DialogueNode,
  versionSnapshot: jsonb, startedAt: datetime,
  completedAt: datetime? }
DialogueAttempt { id: uuid, sessionId: uuid -> DialogueSession,
  choiceId: uuid -> DialogueChoice, idempotencyKey: uuid,
  submittedAt: datetime }
CompletionCard { id: uuid, userId: uuid -> User,
  unitId: uuid -> Unit, issuedAt: datetime, verificationCode: string }
კოლექციას ემატება მხოლოდ იმავე მომხმარებლისა და კურსის სიტყვები. სესიის საჯარო DTO არ შეიცავს
privateEvaluationSnapshot-ს. დიალოგის პასუხი ეკუთვნის მიმდინარე კვანძს, გადასვლა კი იმავე სცენარს.

## PDF page 16

16. მოდელები - ჯგუფები და სისტემური ნაწილი
DailyActivity { userId: uuid -> User, localDate: date,
  earnedXp: int, completedLessons: int, goalReached: bool }
XpTransaction { id: uuid, userId: uuid -> User,
  courseId: uuid -> Course, sourceType: string, sourceId: uuid,
  amount: int, earnedAt: datetime }
Achievement { id: uuid, code: string, title: string,
  ruleType: string, threshold: int }
UserAchievement { userId: uuid -> User,
  achievementId: uuid -> Achievement, earnedAt: datetime }
Challenge { id: uuid, title: string, ruleType: string,
  target: int, rewardXp: int, startsAt: datetime, endsAt: datetime }
UserChallenge { userId: uuid -> User, challengeId: uuid -> Challenge,
  progress: int, completedAt: datetime?, rewardedAt: datetime? }
Classroom { id: uuid, teacherId: uuid -> User,
  courseId: uuid -> Course, name: string, isArchived: bool }
ClassMembership { classroomId: uuid -> Classroom,
  userId: uuid -> User, joinedAt: datetime, status: string }
ClassInvitation { id: uuid, classroomId: uuid -> Classroom,
  codeHash: string, expiresAt: datetime, maxUses: int, usedCount: int }
Assignment { id: uuid, classroomId: uuid -> Classroom,
  title: string, dueAt: datetime, minimumScore: int }
AssignmentLesson { assignmentId: uuid -> Assignment,
  lessonId: uuid -> Lesson }
AssignmentSubmission { id: uuid, assignmentId: uuid -> Assignment,
  userId: uuid -> User, sessionId: uuid -> LearningSession,
  submittedAt: datetime, isLate: bool }
TeacherFeedback { id: uuid, submissionId: uuid -> AssignmentSubmission,
  teacherId: uuid -> User, message: text, createdAt: datetime }
Notification { id: uuid, userId: uuid -> User, type: string,
  title: string, body: text, readAt: datetime?, createdAt: datetime }
ReminderPreference { userId: uuid -> User, channel: string,
  enabled: bool, localTime: time, weekDays: jsonb }
ReminderDelivery { id: uuid, userId: uuid -> User, localDate: date,
  type: string, status: string, sentAt: datetime? }
ContentReport { id: uuid, reporterId: uuid -> User,
  resourceType: string, resourceId: uuid, versionId: uuid?,
  reason: string, comment: text, status: string,
  resolvedBy: uuid? -> User, resolution: text? }
ImportJob { id: uuid, authorId: uuid -> User, status: string,
  fileKey: string, validationErrors: jsonb, createdAt: datetime }
AuditLog { id: uuid, actorId: uuid? -> User, action: string,
  entityType: string, entityId: string, reason: text?,
  traceId: string, occurredAt: datetime }
SystemSetting { key: string, value: jsonb,
  updatedBy: uuid -> User, updatedAt: datetime }

## PDF page 17

17. SQL დიზაინი და Entity Framework
კავშირები და ნორმალიზაცია
მომხმარებელი უკავშირდება როლებს UserRole-ით, კურსებს Enrollment-ით და კლასებს
ClassMembership-ით. კურსს აქვს ერთეულები, გაკვეთილები, უნარები, სიტყვები და დიალოგები.
გაკვეთილის ვერსიას აქვს სავარჯიშოები და რეცენზიები. სასწავლო სესია უკავშირდება
მომხმარებელსა და სავარჯიშოებს; პასუხები ეკუთვნის კონკრეტულ სესიის სავარჯიშოს.
არსებითი კავშირები ინახება გარე გასაღებებით. JSONB გამოიყენება მხოლოდ მოქნილი
სავარჯიშოების მონაცემების, შეფასების წესებისა და ვერსიის ასლებისთვის. მომხმარებელი, კურსი,
პროგრესი და უფლებები წარმოდგენილია ნორმალიზებული ცხრილებით.
ინდექსები და შეზღუდვები
• უნიკალურია normalizedEmail, ენის code და კურსის ენების წყვილი.
• UserRole, Enrollment, LessonProgress, UserAchievement და ClassMembership იყენებს უნიკალურ
წყვილებს.
• SavedWord უნიკალურია (userId, vocabularyItemId); DailyActivity - (userId, localDate).
• Unit/Lesson/Exercise რიგითობა უნიკალურია შესაბამის მშობელ ელემენტში.
• XpTransaction წყაროს უნიკალურობა და პასუხის idempotency key იცავს დუბლიკატისგან.
• ქულები, ინტერვალები და რიგითობა ექვემდებარება მნიშვნელობების შეზღუდვებს.
• ფილტრაციის ინდექსებია courseId, userId, dueAt, status და createdAt საჭირო კომბინაციებით.
EF Core და მიგრაციები
Entity-ებს აქვს IEntityTypeConfiguration კონფიგურაციები. Fluent API განსაზღვრავს გასაღებებს,
კავშირებს, ტიპებს, სიგრძესა და წაშლის ქცევას. DbContext გამოიყენება dependency injection-ით.
წაკითხვისას გამოიყენება AsNoTracking, DTO პროექცია და გვერდებად დაყოფა; ოპერაციები
ასინქრონულია.
მიგრაციები ვერსიონირებულია Git-ში. საწყისი მიგრაცია ქმნის სქემას; შემდგომი მიგრაციები ასახავს
ცვლილებებს. schema.sql გენერირდება მიგრაციებიდან. seed.sql ავსებს როლებს, ენებს, კურსებსა და
საწყის მასალას. ადმინისტრატორის პაროლი სკრიპტში არ ინახება.
მთლიანობა და წაშლის პოლიტიკა
გამოყენებული მასალა არქივირდება და ისტორიული ვერსია რჩება სესიებისთვის. კოლექციის წაშლა
არ შლის საერთო ლექსიკონს. წინაპირობების ციკლები და დიალოგების გადასვლები მოწმდება
გამოქვეყნებამდე. კრიტიკული ოპერაციები შესრულდება ტრანზაქციითა და კონკურენტული
განახლების შემოწმებით.

## PDF page 18

18. ტესტირება, logging და გამოქვეყნება
ტესტირების დაფარვა
• რეგისტრაცია: ვალიდაცია, დუბლიკატი ელფოსტა, ჰეშირება და როლის უსაფრთხო მინიჭება.
• ავტორიზაცია: არასწორი შესვლა, ვადაგასული ტოკენი, განახლება და სესიის გაუქმება.
• წვდომა: სხვისი სესია/ლექსიკონი, სხვა კლასი და ენით შეზღუდული კონტენტი.
• სწავლა: ალტერნატიული პასუხები, სიმბოლოები, წინაპირობები, გულები და ვერსიის ასლი.
• XP: ერთდროული დასრულება, დუბლიკატი მოთხოვნა და ერთჯერადი ჯილდო.
• დრო: streak, დღიური მიზანი და შეხსენება დროის სარტყლის საზღვრებზე.
• ლექსიკონი: დუბლიკატი, გამეორების გრაფიკი და კოლექციის მფლობელი.
• დიალოგი: კვანძის პასუხი, დაუშვებელი გადასვლა და დასრულება.
• კონტენტი: დამტკიცების ეტაპები, საკუთარი მასალის რეცენზიის აკრძალვა და იმპორტი.
Unit ტესტები იყენებს xUnit-სა და FluentAssertions-ს; დროის წესები მოწმდება კონტროლირებადი
საათით. API ტესტები იყენებს WebApplicationFactory-ს და Testcontainers PostgreSQL-ს რეალური SQL
შეზღუდვებისთვის. Angular ტესტები ამოწმებს ფორმებს, სავარჯიშოებსა და state ცვლილებებს.
Playwright ამოწმებს რეგისტრაცია-გაკვეთილი-პროგრესისა და ავტორი-რეცენზენტი-გამოქვეყნების
გზებს.
Error handling და logging
Global exception handler აბრუნებს ProblemDetails-სა და traceId-ს. ILogger წერს მოთხოვნის
ხანგრძლივობას, შეცდომის კონტექსტსა და ფონური ამოცანის შედეგს. სენსიტიური ველები იფარება.
ადმინისტრაციული მოქმედებები ინახება აუდიტში. readiness ამოწმებს ბაზასთან კავშირს; liveness -
სერვისის აქტიურობას.
CI/CD და ექსპლუატაცია
GitHub Actions ასრულებს backend restore/build/test-ს, frontend npm ci/lint/test/build-სა და მნიშვნელოვან
E2E სცენარებს. ცვლილებები გადის pull request-სა და მიმოხილვას. განთავსებისას გამოიყენება Docker
კონტეინერი, კონტროლირებადი მიგრაცია და health შემოწმება.
Nginx ემსახურება Angular-ის ფაილებს და API-ს გადასცემს შესაბამის მოთხოვნებს. გამოიყენება HTTPS,
CORS, ფაილის ლიმიტები და გარემოს ცვლადები. ბაზის სარეზერვო ასლი და აღდგენის პროცედურა
აღწერილია ინსტრუქციაში. განთავსების წარუმატებლობისას გამოიყენება წინა აპლიკაციის ვერსია და
თავსებადი მიგრაციის სტრატეგია.

## PDF page 19

19. ინსტალაცია და ჩასაბარებელი მასალები
რეპოზიტორიის სტრუქტურა
backend/
  LanguageLearning.Api/       controllers, middleware, auth
  LanguageLearning.Core/      entities, interfaces, rules
  LanguageLearning.Infrastructure/  EF, storage, email
  LanguageLearning.Tests/     unit, integration
frontend/                    Angular application
  e2e/                       Playwright scenarios
database/                    schema.sql, seed.sql
docs/                        architecture, API, presentation
docker-compose.yml
README.md
გარემოს გამართვა და გაშვება
საჭიროა .NET 10 SDK, Angular-თან თავსებადი Node.js/npm, Docker და PostgreSQL. კონფიგურაცია
მოიცავს ConnectionStrings:DefaultConnection, JWT issuer/audience/key-ს, SMTP-სა და S3 პარამეტრებს.
განვითარების დამხმარე სერვისები მიეწოდება Docker Compose-ით; ნიმუშის კონფიგურაცია არ
შეიცავს ნამდვილ პაროლებს.
1. რეპოზიტორიის ჩამოტვირთვა; 2. კონფიგურაციის ნიმუშის შევსება; 3. სერვისების გაშვება; 4.
მიგრაციებისა და seed-ის გამოყენება; 5. API-ისა და frontend-ის გაშვება.
docker compose up -d
cd backend
dotnet restore
dotnet ef database update --project LanguageLearning.Infrastructure
  --startup-project LanguageLearning.Api
dotnet run --project LanguageLearning.Api
# frontend directory:
npm ci
npm start
მიგრაციის ბრძანების ორი სტრიქონი წარმოადგენს ერთ ბრძანებას. პორტები და URL-ები ფიქსირდება
launchSettings.json-ში, Angular-ის კონფიგურაციასა და README-ში. Swagger: API-ის /swagger
მისამართი. ტესტები: dotnet test; frontend/E2E ბრძანებები განსაზღვრულია package.json-ში.
ჩასაბარებელი მასალები
• კოდი Git ისტორიით; README აღწერით, ინსტალაციით, გაშვებითა და ტესტირებით.
• EF მოდელები/მიგრაციები, schema.sql, seed.sql და კავშირების დიაგრამა.
• Swagger/OpenAPI, უფლებების აღწერა და ტესტირების ანგარიში.
• აუდიო/მასალის წყაროები და ადმინისტრირების ინსტრუქცია.
• პრეზენტაცია: რეგისტრაცია, ოთხი კურსი, გაკვეთილი, შედეგები, პრაქტიკა, კლასი, მასალის
გამოქვეყნება, დაცული API და ბაზის კავშირები.

## PDF page 20

20. Free და Premium გამოწერები
გამოწერის გეგმები
LexiGo აერთიანებს Free და Premium გეგმებს. Free უზრუნველყოფს ოთხივე ენის ძირითად კურსებზე,
გაკვეთილებზე, პირად ლექსიკონზე, ძირითად პრაქტიკასა და პროგრესზე წვდომას. Premium
დამატებით მოიცავს შეუზღუდავ გულებს, გაფართოებულ ანალიტიკას, დამატებით სიტუაციურ
დიალოგებსა და პერსონალური პრაქტიკის გაფართოებულ რეჟიმებს.
ძირითადი სწავლის ფუნქციები ხელმისაწვდომია უფასოდ. ფასიანი ფუნქციის გვერდზე მკაფიოდ ჩანს
გეგმის მოთხოვნა, პირობები და შეძენის მოქმედება. Premium-ის უფლებები განისაზღვრება სერვერზე
აქტიური გამოწერით; ინტერფეისში სტატუსის შეცვლა უფლებას არ ანიჭებს.
თვიური და წლიური პაკეტები
Premium ხელმისაწვდომია თვიური და წლიური პერიოდით. გეგმას აქვს ფასი, ვალუტა, ხანგრძლივობა
და ფუნქციების ჩამონათვალი. სატესტო კატალოგში თვიური ფასი არის 9.99 GEL, წლიური - 79.99 GEL.
თანხები გამოიყენება სიმულაციურ ოპერაციებში და რეგულირდება ადმინისტრაციულ პანელში.
შეძენის მომენტში ფასისა და პირობების ასლი ინახება შეკვეთაში.
გამოწერის მართვა
• Free/Premium გეგმების შედარება და პერიოდის არჩევა.
• გამოწერის შეძენა სატესტო ბარათითა და შედეგის დადასტურება.
• აქტიური გეგმის, დაწყების, დასრულებისა და შემდეგი განახლების თარიღის ნახვა.
• ავტომატური სატესტო განახლების ჩართვა ან გამორთვა.
• გაუქმების მოთხოვნა პერიოდის ბოლოს; გადახდილი სატესტო პერიოდი ბოლომდე მოქმედებს.
• ვადაგასვლის შემდეგ Free-ზე დაბრუნება სასწავლო პროგრესის დაკარგვის გარეშე.
განახლება და წარუმატებელი ოპერაცია
ავტომატური განახლება გამოიყენებს მომხმარებლის მიერ არჩეულ აქტიურ სატესტო ბარათს. ფონური
ამოცანა ვადის დადგომისას ქმნის ახალ განახლების შეკვეთას. არასაკმარისი ბალანსის ან დაბლოკილი
ბარათისას ინახება წარუმატებელი მცდელობა და იგზავნება შეტყობინება. წარუმატებელი განახლება
არ აგრძელებს Premium-ს; მომხმარებელს შეუძლია ხელახლა ცდა.
აქტიური თვიური/წლიური გეგმის ცვლილება იგეგმება შემდეგი პერიოდისთვის. ახალი ტარიფი
მიმდინარე პერიოდს არ ცვლის. არსებული გამოწერა და მომავალი ცვლილება ცალკე ჩანს პროფილში;
დუბლიკატი აქტიური გამოწერა არ იქმნება.

## PDF page 21

21. ვირტუალური ბარათები და გადახდები
სატესტო ვირტუალური ბარათი
მომხმარებელი ქმნის აპლიკაციის შიდა სატესტო ბარათს სახელით, სისტემური იდენტიფიკატორით,
საჩვენებელი ბოლო ოთხი ციფრით, ვალუტითა და სიმულაციური ბალანსით. ეს არის გადახდის
სცენარების შესამოწმებელი ინსტრუმენტი: რეალური საბანკო ბარათი არ გამოიცემა და რეალური
თანხა არ ირიცხება.
• სატესტო ბარათის შექმნა და ბარათების სიის ნახვა.
• სახელის, ნიღბიანი ნომრის, ბალანსის, ვალუტისა და სტატუსის ჩვენება.
• საკუთარი ბარათის დაბლოკვა და განბლოკვა.
• ნაგულისხმევი ბარათის არჩევა შემდეგი განახლებისთვის.
• საცდელი ბალანსის შევსება მკაფიოდ მონიშნული მოქმედებით.
• ბარათით შესრულებული ოპერაციების ისტორიის ნახვა და ფილტრაცია.
გადახდის შესრულების პროცესი
მომხმარებელი ირჩევს გეგმას, პერიოდსა და ბარათს. სერვერი ქმნის შეკვეთას ფასის ასლით და
ამოწმებს მფლობელს, ბარათის სტატუსს, ვალუტასა და ბალანსს. დადასტურების შემდეგ თანხის
სატესტო ჩამოჭრა, წარმატებული ოპერაცია და გამოწერის გააქტიურება სრულდება ერთიანი
ტრანზაქციით. ეკრანზე ჩანს ოპერაციის ნომერი, თანხა, გეგმა და სტატუსი.
სიმულაციის შედეგები
მხარდაჭერილია წარმატებული გადახდა, დაბლოკილი ბარათი, არასაკმარისი ბალანსი, ვალუტის
შეუსაბამობა და სატესტო ტექნიკური შეფერხება. უარყოფილი ოპერაცია ინახება ისტორიაში, მაგრამ
ბალანსს არ ცვლის და Premium-ს არ ააქტიურებს. განმეორებითი გაგზავნა იმავე ოპერაციას მეორედ არ
ასრულებს.
ისტორია, ქვითარი და დაბრუნება
ისტორია იფილტრება ბარათით, ტიპით, სტატუსითა და თარიღით. თითოეულ ჩანაწერს აქვს reference,
დრო, თანხა და შესაბამისი შეკვეთა. წარმატებულ გადახდას ახლავს ჩამოსატვირთი სატესტო ქვითარი.
უფლებამოსილი ადმინისტრატორი ასრულებს დასაბუთებულ სიმულირებულ დაბრუნებას: თანხა
ირიცხება იმავე სატესტო ბარათზე და შესაბამისი Premium უფლება უქმდება დაბრუნების
პოლიტიკით.
შევსება, ჩამოჭრა და დაბრუნება ქმნის უცვლელ საბალანსო ჩანაწერებს. დაბლოკილი ბარათი ვერ
ახორციელებს ახალ ჩამოჭრას; თანხის დაბრუნება მასზე დასაშვებია. მომხმარებელი ხედავს მხოლოდ
საკუთარ ბარათებსა და ისტორიას.

## PDF page 22

22. გამოწერების ინტერფეისი და API
გვერდები და მდგომარეობის მართვა
ემატება Pricing, Subscription, Payment Checkout, My Cards, Transaction History და Payment Receipt
გვერდები. Checkout აჩვენებს არჩეულ გეგმას, პერიოდს, თანხას, ვალუტასა და სატესტო გადახდის
აღნიშვნას. ცალკეა დამუშავების, წარმატების, უარყოფისა და გაურკვეველი ქსელური შედეგის
ეკრანები.
BillingStore მართავს გეგმებს, აქტიურ გამოწერას, ბარათებსა და შეკვეთას. PaymentService აგზავნის
მოთხოვნას idempotency key-ით. ქსელის გაწყვეტისას UI ჯერ ამოწმებს ოპერაციის სტატუსს და
მხოლოდ შემდეგ აძლევს განმეორების საშუალებას. Premium ელემენტები განახლდება სერვერისგან
მიღებული entitlement-ებით. frontend/features/billing მოიცავს plans, subscription, cards, checkout და
transactions კომპონენტებს.
მომხმარებლის API
GET  /api/v1/plans
GET  /api/v1/me/subscription
GET  /api/v1/me/entitlements
POST /api/v1/me/subscription/cancel
POST /api/v1/me/subscription/resume
PUT  /api/v1/me/subscription/renewal
PUT  /api/v1/me/subscription/scheduled-plan
GET/POST /api/v1/me/test-cards
PATCH /api/v1/me/test-cards/{id}
POST /api/v1/me/test-cards/{id}/block
POST /api/v1/me/test-cards/{id}/unblock
POST /api/v1/me/test-cards/{id}/top-ups
GET  /api/v1/me/test-cards/{id}/transactions
POST /api/v1/me/billing-orders
GET  /api/v1/me/billing-orders/{id}
POST /api/v1/me/billing-orders/{id}/payments
GET  /api/v1/me/payments/{id}
GET  /api/v1/me/payments/{id}/receipt
GET  /api/v1/me/transactions
ადმინისტრირება
GET/POST /api/v1/admin/plans
PUT /api/v1/admin/plans/{id}
GET /api/v1/admin/subscriptions
GET /api/v1/admin/payments
POST /api/v1/admin/payments/{id}/refunds
GET /api/v1/admin/billing-audit
ბარათები და გადახდები მოითხოვს ავტორიზაციასა და მფლობელის შემოწმებას. გეგმებსა და
დაბრუნებებს მართავს Super Admin ან კონკრეტული BillingManage უფლების მქონე Admin.
მასწავლებელს, ავტორს, რეცენზენტსა და მოდერატორს ფინანსური ადმინისტრირების წვდომა არ
ენიჭება.

## PDF page 23

23. გამოწერებისა და გადახდების მოდელები
SubscriptionPlan { id: uuid, code: string, name: string,
  isActive: bool, createdAt: datetime }
PlanPrice { id: uuid, planId: uuid -> SubscriptionPlan,
  period: string, amount: decimal(18,2), currency: string,
  validFrom: datetime, validTo: datetime? }
Feature { id: int, code: string, description: string }
PlanFeature { planId: uuid -> SubscriptionPlan,
  featureId: int -> Feature, limitValue: int? }
TestCard { id: uuid, userId: uuid -> User, nickname: string,
  displayLastFour: string, currency: string, balance: decimal(18,2),
  status: string, version: long, createdAt: datetime }
BillingPreference { userId: uuid -> User,
  defaultTestCardId: uuid? -> TestCard, autoRenew: bool }
BillingOrder { id: uuid, userId: uuid -> User,
  planPriceId: uuid -> PlanPrice, subscriptionId: uuid?,
  amountSnapshot: decimal(18,2), currency: string,
  periodSnapshot: string, status: string, orderType: string,
  renewalPeriodKey: string?, createdAt: datetime }
PaymentAttempt { id: uuid, orderId: uuid -> BillingOrder,
  testCardId: uuid -> TestCard, status: string, failureCode: string?,
  idempotencyKey: uuid, amount: decimal(18,2), currency: string,
  createdAt: datetime, completedAt: datetime? }
Subscription { id: uuid, userId: uuid -> User,
  planId: uuid -> SubscriptionPlan, status: string,
  currentPeriodStart: datetime, currentPeriodEnd: datetime,
  cancelAtPeriodEnd: bool, scheduledPlanPriceId: uuid?,
  createdAt: datetime }
SubscriptionEvent { id: uuid, subscriptionId: uuid -> Subscription,
  eventType: string, relatedPaymentId: uuid? -> PaymentAttempt,
  occurredAt: datetime, actorId: uuid? -> User }
CardTransaction { id: uuid, testCardId: uuid -> TestCard,
  paymentId: uuid? -> PaymentAttempt, refundId: uuid? -> Refund,
  type: string, amount: decimal(18,2), balanceAfter: decimal(18,2),
  reference: string, createdAt: datetime }
Refund { id: uuid, paymentId: uuid -> PaymentAttempt,
  amount: decimal(18,2), reason: string, status: string,
  idempotencyKey: uuid, createdBy: uuid -> User,
  completedAt: datetime? }
PaymentReceipt { id: uuid, paymentId: uuid -> PaymentAttempt,
  receiptNumber: string, issuedAt: datetime }
BillingJobExecution { id: uuid, subscriptionId: uuid -> Subscription,
  periodKey: string, status: string, lastAttemptAt: datetime,
  nextAttemptAt: datetime? }
Feature-ის მაგალითებია UnlimitedHearts, AdvancedAnalytics და ExtraDialogues. Free/Premium უფლებები
მიიღება გეგმისა და გამოწერის სტატუსისგან. თანხები ინახება decimal ტიპით და არა floating point-ით.
სატესტო ბარათში არ ინახება ნამდვილი PAN ან CVV.
კავშირები: მომხმარებელს აქვს ბარათები, შეკვეთები და გამოწერის ისტორია; შეკვეთას - გადახდის
მცდელობები; წარმატებული გადახდა უკავშირდება გამოწერას, ბარათის ჩანაწერსა და ქვითარს. ყველა
ფინანსური ცვლილება აღირიცხება SubscriptionEvent-სა და AuditLog-ში.

## PDF page 24

24. გადახდების უსაფრთხოება და ტესტირება
მდგომარეობები და მთლიანობა
ბარათის სტატუსებია Active და Blocked. შეკვეთის - Created, Paid, Cancelled და Expired; გადახდის -
Pending, Succeeded და Failed; გამოწერის - Active, Expired და Refunded. პერიოდის ბოლოს გაუქმება
ინახება ცალკე cancelAtPeriodEnd ნიშნით, ამიტომ წინასწარ არ წყვეტს მოქმედ წვდომას.
აქტიური პერიოდი განისაზღვრება UTC საზღვრებით: start <= now < end. მომხმარებელს
ერთდროულად აქვს მაქსიმუმ ერთი აქტიური Premium გამოწერა. განახლების შეკვეთა უნიკალურია
გამოწერისა და პერიოდის მიხედვით. სატესტო გადახდის წარმატება დასაშვებია შეკვეთაზე მხოლოდ
ერთხელ.
ტრანზაქცია და ერთდროული ოპერაციები
ბალანსის შემცირება, CardTransaction, PaymentAttempt-ის წარმატება, შეკვეთის დახურვა და გამოწერის
გააქტიურება სრულდება SQL ტრანზაქციაში. ბარათის ვერსია და ატომური ბალანსის შემოწმება იცავს
ერთდროული ჩამოჭრისგან. ბალანსი არასდროს ხდება უარყოფითი. დაბრუნებული თანხების ჯამი
ვერ აღემატება წარმატებულად გადახდილ თანხას.
ფასი, პერიოდი და ვალუტა აიღება სერვერის კატალოგიდან. კლიენტის გამოგზავნილი თანხა არ
გამოიყენება საბოლოო ფასად. POST მოქმედებებს ახლავს მომხმარებლისა და ოპერაციის ფარგლებში
უნიკალური idempotency key. ერთსა და იმავე გასაღებს განსხვავებული შიგთავსით API უარყოფს
კონფლიქტით.
დაცვა და აღრიცხვა
ყველა ქმედება ამოწმებს ავტორიზაციას, მფლობელსა და სტატუსს. ბარათის შევსება მკაფიოდ
სატესტოა და აქვს ოპერაციის ლიმიტი. ბარათის/გადახდის შეცდომები არ ამჟღავნებს სხვა ანგარიშის
არსებობას. საბალანსო ისტორია არ რედაქტირდება: შესწორება კეთდება ახალი ჩანაწერით.
ადმინისტრაციულ დაბრუნებას სჭირდება მიზეზი და აუდიტი.
ტესტირების სცენარები
• ბარათის შექმნა, სიის ნახვა, დაბლოკვა და განბლოკვა.
• სხვისი ბარათის გამოყენების ან ისტორიის წაკითხვის აკრძალვა.
• წარმატებული გადახდა და Premium-ის მყისიერი გააქტიურება.
• დაბლოკილი ბარათი, არასაკმარისი ბალანსი და ვალუტის შეუსაბამობა.
• ერთი მოთხოვნის გამეორება და ორი ერთდროული გადახდა.
• ტრანზაქციის ჩავარდნისას ბალანსისა და გამოწერის უკუცვლა.
• განახლება, გაუქმება, ვადაგასვლა და დაგეგმილი ტარიფის ცვლილება.
• დაბრუნება, დაბრუნების დუბლიკატი და თანხის ზედა ზღვარი.
• Premium ფუნქციის API დაცვა, Free-ზე დაბრუნებისას პროგრესის შენარჩუნება.
README აღწერს სატესტო ბარათების შექმნას, გეგმის seed მონაცემებსა და ფონური განახლების
გაშვებას. პრეზენტაცია აჩვენებს როგორც წარმატებულ, ისე უარყოფილ გადახდას, ბარათის
დაბლოკვას, ისტორიასა და გამოწერის მართვას.

## PDF page 25

25. კრიტერიუმების დაფარვა და ბონუსები
Frontend - 40 ქულა
• რესფონსიული HTML/CSS (10): ტელეფონი, პლანშეტი, კომპიუტერი და თემები.
• Angular კომპონენტები (10): features/core/shared სტრუქტურა და საერთო UI ელემენტები.
• ფუნქციური ინტერფეისი (10): სწავლა, აუდიო, ლექსიკონი, კლასები და ადმინისტრირება.
• State management (10): Signals, computed state, ქსელური სტატუსები და სესიის აღდგენა.
Backend - 40 ქულა
• SQL დიზაინი (10): ნორმალიზებული მოდელები, კავშირები, ინდექსები და ტრანზაქციები.
• EF მოდელები/მიგრაციები (10): Fluent კონფიგურაციები და ვერსიონირებული სქემა.
• REST API (10): DTO-ები, ვალიდაცია, ფილტრები, გვერდებად დაყოფა და OpenAPI.
• ავთენტიფიკაცია/ავტორიზაცია (10): Identity, JWT, სესიები, როლები და რესურსის მფლობელი.
დამატებითი - 20 ქულა
• კოდის ხარისხი/დოკუმენტაცია (10): არქიტექტურა, ტესტები, README, Swagger და SQL სკრიპტები.
• პრეზენტაცია (10): პროდუქტის სრული გზა და ტექნიკური ნაწილის დემონსტრაცია.
სავალდებულო ტექნიკური და ჩაბარების ნაწილი
Git; Swagger; unit ტესტები; error handling; logging; სრული README; დაყენებისა და გაშვების
ინსტრუქცია; მონაცემთა ბაზის სკრიპტები და პრეზენტაცია წარმოდგენილია შესაბამისი ფუნქციებითა
და ცალკე ჩასაბარებელი მასალებით.
ბონუსის ოთხივე კატეგორია
დამატებითი ფუნქციები: ოთხი ენა, კლასები/დავალებები, შეტყობინებები, მიღწევები, გამოწვევები,
Free/Premium გეგმები, სატესტო ბარათები, სიმულირებული გადახდები და ოპერაციების ისტორია.
კარგი UI/UX: რუკა, თემები, რესფონსიულობა, კლავიატურა და უკუკავშირი. მაღალი კოდის ხარისხი:
DTO-ები, ტესტები, ვალიდაცია, ტრანზაქციები და CI/CD. ინოვაციური გადაწყვეტილებები:
პერსონალური პრაქტიკა, ინტერვალებით გამეორება და განშტოებული დიალოგები.
საკუთარი გამორჩეული დამატებები
სუსტი უნარების რუკა; რეკომენდაციის მიზეზის ჩვენება; კონტენტის რეცენზირება და ვერსიები; ენით
შეზღუდული ავტორები; მასწავლებლის ანალიტიკა; სიტყვების კოლექციები; სიმბოლოების
კლავიატურა; საკუთარი ხმის მოსმენა; კვირის შეჯამება და მიღწევის ბარათი.