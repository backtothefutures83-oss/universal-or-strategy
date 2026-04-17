import { useState } from 'react';
import { Layout } from './components/Layout';
import { Overview } from './components/views/Overview';
import { MultimodalPipes } from './components/views/MultimodalPipes';
import { JitterFreeRedundancy } from './components/views/JitterFreeRedundancy';
import { AtomicConstant } from './components/views/AtomicConstant';

function App() {
  const [activeView, setActiveView] = useState('overview');

  const renderView = () => {
    switch (activeView) {
      case 'overview':
        return <Overview />;
      case 'multimodal':
        return <MultimodalPipes />;
      case 'mirror':
        return <JitterFreeRedundancy />;
      case 'atomic':
        return <AtomicConstant />;
      default:
        return <Overview />;
    }
  };

  return (
    <Layout activeView={activeView} setActiveView={setActiveView}>
      {renderView()}
    </Layout>
  );
}

export default App;