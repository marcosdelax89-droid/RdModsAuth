# 🌐 Site Web BelgaAuth - Guia Completo

## 📋 Visão Geral

Agora você tem um site web completo e moderno para hospedar seu sistema KeyAuth! O site inclui:

### ✨ Funcionalidades

1. **Página Inicial (Landing Page)** - `/Home`
   - Design moderno e profissional
   - Apresentação do sistema
   - Botões de login e registro

2. **Página de Login** - `/Login`
   - Interface limpa e intuitiva
   - Login com username e senha
   - Integração com a API KeyAuth

3. **Página de Registro** - `/Register`
   - Criação de conta com licença
   - Validação de campos
   - Integração com a API KeyAuth

4. **Dashboard do Usuário** - `/UserDashboard`
   - Área logada para usuários finais
   - Visualização de informações da conta
   - Gerenciamento de subscriptions

5. **Painel Administrativo** - `/` (páginas admin)
   - Dashboard com estatísticas
   - Gerenciamento de usuários
   - Gerenciamento de licenças
   - Criação de usuários e licenças
   - Edição de usuários

## 🚀 Como Iniciar

### 1. Executar a API

```bash
cd BelgaAuthAPI
dotnet run
```

O site estará disponível em: `http://localhost:5000`

### 2. Acessar o Site

- **Página Inicial**: http://localhost:5000/Home
- **Login**: http://localhost:5000/Login
- **Registro**: http://localhost:5000/Register
- **Dashboard Usuário**: http://localhost:5000/UserDashboard
- **Painel Admin**: http://localhost:5000/ (Dashboard Admin)

## 🎨 Estrutura do Site

```
BelgaAuthAPI/
├── Pages/
│   ├── Home.cshtml              # Página inicial pública
│   ├── Login.cshtml             # Página de login
│   ├── Register.cshtml          # Página de registro
│   ├── UserDashboard.cshtml     # Dashboard do usuário
│   ├── Index.cshtml             # Dashboard admin
│   ├── Users.cshtml             # Gerenciar usuários (admin)
│   ├── Licenses.cshtml          # Gerenciar licenças (admin)
│   ├── CreateUser.cshtml        # Criar usuário (admin)
│   ├── CreateLicense.cshtml     # Criar licença (admin)
│   ├── EditUser.cshtml          # Editar usuário (admin)
│   └── _Layout.cshtml           # Layout do painel admin
├── wwwroot/
│   ├── css/
│   │   └── site.css             # Estilos customizados
│   └── js/
│       └── site.js              # JavaScript customizado
└── Controllers/
    ├── AuthController.cs        # API de autenticação
    └── AdminController.cs       # API administrativa
```

## 📱 Páginas Disponíveis

### Públicas

- **`/Home`** - Página inicial com landing page moderna
- **`/Login`** - Login para usuários
- **`/Register`** - Registro de novos usuários com licença

### Área do Usuário

- **`/UserDashboard`** - Dashboard do usuário logado
  - Informações da conta
  - Visualização de subscriptions
  - Status da conta

### Painel Administrativo

- **`/`** - Dashboard principal (admin)
- **`/Users`** - Lista de todos os usuários
- **`/Licenses`** - Lista de todas as licenças
- **`/CreateUser`** - Criar novo usuário
- **`/CreateLicense`** - Gerar nova licença
- **`/EditUser?id={id}`** - Editar usuário específico

## 🎯 Fluxo de Uso

### Para Usuários Finais:

1. Acesse a página inicial (`/Home`)
2. Clique em "Criar Conta" ou vá para `/Register`
3. Preencha o formulário com username, senha e chave de licença
4. Após criar a conta, faça login em `/Login`
5. Acesse seu dashboard em `/UserDashboard`

### Para Administradores:

1. Acesse o painel admin em `/`
2. Veja estatísticas no dashboard
3. Crie usuários em `/CreateUser`
4. Gere licenças em `/CreateLicense`
5. Gerencie usuários em `/Users`
6. Gerencie licenças em `/Licenses`

## 🎨 Design e Estilização

O site utiliza:

- **Bootstrap 5.3** - Framework CSS responsivo
- **Bootstrap Icons** - Ícones modernos
- **CSS Customizado** - Estilos personalizados em `wwwroot/css/site.css`
- **JavaScript Customizado** - Funções utilitárias em `wwwroot/js/site.js`

### Cores Principais:

- Gradiente primário: `#667eea` → `#764ba2`
- Fundo escuro: `#1e293b`
- Cards brancos com sombras suaves

## 🔧 Personalização

### Alterar Cores:

Edite o arquivo `wwwroot/css/site.css` e modifique as variáveis CSS:

```css
:root {
    --primary-gradient: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    --dark-bg: #1e293b;
    /* ... */
}
```

### Adicionar Logo:

1. Adicione sua logo em `wwwroot/images/`
2. Atualize o logo nas páginas ou no `_Layout.cshtml`

### Configurar URL da API:

As páginas de login/registro fazem chamadas para `/api/auth`. Certifique-se de que a API está configurada corretamente no `Program.cs`.

## 📝 Notas Importantes

1. **Autenticação**: As páginas de login e registro fazem chamadas à API KeyAuth. Certifique-se de que a API está funcionando corretamente.

2. **Sessões**: O dashboard do usuário precisa de autenticação. Implemente sistema de sessões/cookies conforme necessário.

3. **Segurança**: Para produção, adicione:
   - Autenticação no painel admin
   - HTTPS
   - Rate limiting
   - Validação CSRF

4. **Banco de Dados**: O SQLite será criado automaticamente na primeira execução (`auth.db`).

## 🚀 Próximos Passos

- [ ] Implementar sistema de sessões para usuários logados
- [ ] Adicionar autenticação no painel admin
- [ ] Implementar recuperação de senha
- [ ] Adicionar página de perfil do usuário
- [ ] Implementar histórico de atividades
- [ ] Adicionar notificações em tempo real

## 🐛 Troubleshooting

### Página não carrega:
- Verifique se a API está rodando
- Verifique a porta (padrão: 5000)
- Verifique se o `Program.cs` está configurado corretamente

### Erro de CSS/JS:
- Verifique se os arquivos estão em `wwwroot/css/` e `wwwroot/js/`
- Verifique se o `UseStaticFiles()` está habilitado no `Program.cs`

### Erro ao fazer login:
- Verifique se a API KeyAuth está respondendo corretamente
- Verifique os logs da aplicação
- Teste os endpoints da API manualmente

## 📞 Suporte

Se precisar de ajuda, verifique:
- Logs da aplicação no console
- Respostas da API usando Swagger: `http://localhost:5000/swagger`
- Banco de dados SQLite: `auth.db`

---

**BelgaAuth** - Sistema de Autenticação KeyAuth Completo 🚀






