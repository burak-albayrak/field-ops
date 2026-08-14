import './App.css'
import { AppHeader } from './components/common/AppHeader'
import { VisitListSection } from './components/visits/VisitListSection'

function App() {
  return (
    <div className="app">
      <AppHeader />
      <main className="app-main">
        <VisitListSection />
      </main>
    </div>
  )
}

export default App
