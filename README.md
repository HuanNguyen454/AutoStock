# ASM_AutoStock

ASM_AutoStock la goi deploy MVC-only duoc tach rieng tu `Final_project` de publish ra folder local va upload thu cong len MonsterASP.

## Cau truc

- `src/ASM.WebPortal`: website MVC duy nhat cho Admin, Owner, Manager, Staff
- `src/ASM.Domain`: entities, enums, role constants
- `src/ASM.Application`: DTOs va service interfaces
- `src/ASM.Infrastructure`: DbContext, services, auth, migrations, seed data
- `tests/ASM.Tests`: test cho auth va order workflow

## Database

- Tiep tuc dung lai SQL Server schema va migrations hien co
- Connection string duoc doc tu `ConnectionStrings__DefaultConnection`
- App production khong can API rieng hay `Api:BaseUrl`

## Tai khoan demo local

- `admin@asm.local` / `Asm123$`
- `owner@asm.local` / `Asm123$`
- `manager@asm.local` / `Asm123$`
- `staff@asm.local` / `Asm123$`

## Chay local

```powershell
dotnet run --project src/ASM.WebPortal
```

## Build va test

```powershell
dotnet build ASM_AutoStock.sln
dotnet test ASM_AutoStock.sln
```

## Publish ra folder local

Publish profile da duoc cau hinh de xuat vao `publish\MonsterAsp`.

```powershell
dotnet publish src/ASM.WebPortal/ASM.WebPortal.csproj -c Release /p:PublishProfile=MonsterAspFolder
```

Sau khi publish xong, upload noi dung trong thu muc `publish\MonsterAsp` len hosting MonsterASP bang FTP/File Manager.

## Cau hinh production goi y

- `App__UseHttpsRedirection=false`
- `App__ApplyMigrationsOnStartup=true`
- `App__SeedSystemData=true`
- `App__SeedDemoData=false`
- `AllowedHosts=*`

## Environment variables can dat tren MonsterASP

- `ConnectionStrings__DefaultConnection`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__SigningKey`
- `AllowedHosts`

## Ghi chu

- Ung dung moi khong expose `/api/*`
- Cookie authentication la co che dang nhap chinh
- Khong dung WebDeploy trong package nay
