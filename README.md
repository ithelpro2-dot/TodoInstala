# TodoInstala para Windows

TodoInstala permite seleccionar varios programas e instalarlos en una sesión de
Windows. Revisa la selección antes de pedir el permiso de administrador.
Los programas se descargan desde las fuentes del catálogo; sus licencias
pertenecen a cada proveedor.

## Compilar

En Windows, ejecuta `powershell -NoProfile -File tools/build-installer.ps1`.
El script usa el compilador de .NET Framework de Windows y genera
`installer/build/CatalogoBase.exe`. Al abrirlo puedes elegir los programas
en el selector. Para retirar alguno, usa **Configuración → Aplicaciones**
o el procedimiento de desinstalación del fabricante.

## Code signing policy

La integración con SignPath Foundation está preparada y pendiente de admisión.
GitHub Actions compila desde este repositorio. La opción de firmar solicita la
firma de la versión en SignPath y espera la aprobación de un responsable del
proyecto. La compilación y las solicitudes de firma se inician manualmente.

La configuración de artefacto está en `signpath/artifact.xml`. Configura un
proyecto con verificación de origen para este repositorio y una política de
publicación con aprobación manual. Guarda el token de un usuario con permiso
de solicitud como secreto de Actions `SIGNPATH_API_TOKEN`. Usa las variables
`SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_PROJECT_SLUG`,
`SIGNPATH_SIGNING_POLICY_SLUG` y `SIGNPATH_ARTIFACT_CONFIGURATION_SLUG`
para los valores del proyecto aprobado.

El ejecutable solicita descargas a los fabricantes, Microsoft Store, WinGet
u otros proveedores según los programas elegidos. Esos servicios reciben los
datos de conexión necesarios y aplican sus propias políticas de privacidad.

El código de este repositorio se publica con la licencia MIT.
